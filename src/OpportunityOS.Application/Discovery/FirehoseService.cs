using Microsoft.Extensions.Logging;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Discovery;

public interface IFirehoseService
{
    Task<SearchCampaignResponse> CreateCampaignAsync(CreateCampaignRequest req, CancellationToken ct);
    Task<IReadOnlyList<SearchCampaignResponse>> GetCampaignsAsync(CancellationToken ct);
    Task<SearchCampaignResponse?> GetCampaignAsync(Guid id, CancellationToken ct);
    Task<FirehoseRunResponse> QuickSearchAsync(QuickSearchRequest req, CancellationToken ct);
    Task<FirehoseRunResponse> AggressiveSearchAsync(AggressiveSearchRequest req, CancellationToken ct);
    Task<IReadOnlyList<RawJobCandidateResponse>> GetRawCandidatesAsync(int take, CancellationToken ct);
}

/// <summary>
/// The Firehose: massive candidate collection. Runs queries verbatim across raw search
/// providers, saves every result as a RawJobCandidate BEFORE strong filtering (collects
/// broadly, classifies later). No LLM. Dedups obvious URL repeats. Audits every query
/// (SearchQueryExecution) and every run (ExecutionRun).
/// </summary>
public sealed class FirehoseService : IFirehoseService
{
    private const int DefaultQuickTake = 20;
    private const int DefaultAggressiveMaxQueries = 200;
    private const int DefaultMaxResultsPerQuery = 20;

    private readonly IEnumerable<IRawSearchProvider> _providers;
    private readonly QueryExpansionService _expansion;
    private readonly IFirehoseStore _store;
    private readonly ILogger<FirehoseService> _logger;

    public FirehoseService(
        IEnumerable<IRawSearchProvider> providers, QueryExpansionService expansion,
        IFirehoseStore store, ILogger<FirehoseService> logger)
    {
        _providers = providers;
        _expansion = expansion;
        _store = store;
        _logger = logger;
    }

    public async Task<SearchCampaignResponse> CreateCampaignAsync(CreateCampaignRequest req, CancellationToken ct)
    {
        var campaign = new SearchCampaign(
            req.Name, req.Description ?? string.Empty, ParsePriority(req.Priority),
            req.BaseKeywords ?? new(), req.TargetSources, req.ExcludedDomains,
            req.DailyQueryBudget ?? 250);
        await _store.AddCampaignAsync(campaign, ct);
        await _store.SaveChangesAsync(ct);
        return ToResponse(campaign);
    }

    public async Task<IReadOnlyList<SearchCampaignResponse>> GetCampaignsAsync(CancellationToken ct) =>
        (await _store.GetCampaignsAsync(ct)).Select(ToResponse).ToList();

    public async Task<SearchCampaignResponse?> GetCampaignAsync(Guid id, CancellationToken ct)
    {
        var c = await _store.GetCampaignAsync(id, ct);
        return c is null ? null : ToResponse(c);
    }

    public async Task<FirehoseRunResponse> QuickSearchAsync(QuickSearchRequest req, CancellationToken ct)
    {
        var take = req.Take is > 0 ? req.Take!.Value : DefaultQuickTake;
        var save = req.SaveRawCandidates ?? true;
        var run = ExecutionRun.Start("FirehoseQuickSearch");
        await _store.AddExecutionRunAsync(run, ct);

        var stats = new RunStats();
        await RunQueryAsync(Guid.Empty, req.Query, take, save, run, stats, ct);

        run.Complete();
        await _store.SaveChangesAsync(ct);
        return new FirehoseRunResponse(run.Id, run.Status.ToString(), null,
            stats.Queries, stats.Results, stats.New, stats.Duplicates, run.ItemsFailed);
    }

    public async Task<FirehoseRunResponse> AggressiveSearchAsync(AggressiveSearchRequest req, CancellationToken ct)
    {
        var maxQueries = req.MaxQueries is > 0 ? req.MaxQueries!.Value : DefaultAggressiveMaxQueries;
        var maxResults = req.MaxResultsPerQuery is > 0 ? req.MaxResultsPerQuery!.Value : DefaultMaxResultsPerQuery;
        var save = req.SaveRawCandidates ?? true;

        SearchCampaign? campaign = null;
        IEnumerable<string> seeds = Array.Empty<string>();
        if (req.CampaignId is { } cid)
        {
            campaign = await _store.GetCampaignAsync(cid, ct);
            if (campaign is null) throw new InvalidOperationException($"Campaign {cid} not found.");
            seeds = campaign.BaseKeywords;
            maxQueries = Math.Min(maxQueries, campaign.DailyQueryBudget);
        }

        var queries = _expansion.ExpandAggressive(seeds, maxQueries);
        var run = ExecutionRun.Start("FirehoseAggressive");
        await _store.AddExecutionRunAsync(run, ct);

        if (req.PromoteAutomatically == true)
            _logger.LogInformation("Aggressive search: automatic promotion requested but deferred to the qualification pipeline (later phase).");

        var stats = new RunStats();
        foreach (var query in queries)
        {
            await RunQueryAsync(campaign?.Id ?? Guid.Empty, query, maxResults, save, run, stats, ct);
            await _store.SaveChangesAsync(ct); // persist progress incrementally
        }

        campaign?.MarkRun();
        run.Complete();
        await _store.SaveChangesAsync(ct);
        _logger.LogInformation("FirehoseAggressive done: queries={Q} results={R} new={N} dup={D} errors={E}",
            stats.Queries, stats.Results, stats.New, stats.Duplicates, run.ItemsFailed);
        return new FirehoseRunResponse(run.Id, run.Status.ToString(), campaign?.Id,
            stats.Queries, stats.Results, stats.New, stats.Duplicates, run.ItemsFailed);
    }

    public async Task<IReadOnlyList<RawJobCandidateResponse>> GetRawCandidatesAsync(int take, CancellationToken ct) =>
        (await _store.GetRawCandidatesAsync(Math.Clamp(take, 1, 1000), ct)).Select(ToResponse).ToList();

    private async Task RunQueryAsync(
        Guid campaignId, string query, int maxResults, bool save, ExecutionRun run, RunStats stats, CancellationToken ct)
    {
        foreach (var provider in _providers.Where(p => p.IsAvailable))
        {
            stats.Queries++;
            var exec = SearchQueryExecution.Start(campaignId, query, provider.ProviderName);
            await _store.AddQueryExecutionAsync(exec, ct);
            try
            {
                var results = await provider.SearchAsync(query, maxResults, ct);
                int newCount = 0, dupCount = 0;
                foreach (var r in results)
                {
                    stats.Results++;
                    if (string.IsNullOrWhiteSpace(r.Url)) continue;
                    if (!stats.SeenUrls.Add(r.Url)) { dupCount++; stats.Duplicates++; continue; }
                    if (!save) continue;
                    if (await _store.RawCandidateExistsByUrlAsync(r.Url, ct)) { dupCount++; stats.Duplicates++; continue; }

                    var cls = ClassifySource(r.Url);
                    var candidate = new RawJobCandidate(
                        r.Title, r.Url, provider.ProviderName, cls.SourceName, cls.Type,
                        cls.Confidence, cls.RequiresManualValidation, r.Snippet,
                        publishedAtUtc: r.PublishedAtUtc,
                        searchCampaignId: campaignId == Guid.Empty ? null : campaignId,
                        query: query);
                    await _store.AddRawCandidateAsync(candidate, ct);
                    newCount++; stats.New++;
                }
                exec.Succeed(results.Count, newCount, dupCount);
                run.RecordSuccess();
            }
            catch (Exception ex)
            {
                exec.Fail(ex.Message);
                run.RecordFailure($"{provider.ProviderName}: {ex.Message}");
                _logger.LogWarning(ex, "Firehose query failed [{Provider}] {Query}", provider.ProviderName, query);
            }
        }
    }

    /// <summary>
    /// Lightweight host-based source classification. The full ISourceClassifierService
    /// (Priority 4) will replace this; here we just give every candidate a sane SourceType.
    /// </summary>
    private static (SourceType Type, string SourceName, int Confidence, bool RequiresManualValidation) ClassifySource(string url)
    {
        var host = TryHost(url);
        bool Has(params string[] needles) => needles.Any(n => host.Contains(n, StringComparison.OrdinalIgnoreCase));

        if (Has("greenhouse.io", "lever.co", "ashbyhq.com", "smartrecruiters.com", "workdayjobs.com"))
            return (SourceType.OfficialAts, host, 90, false);
        if (Has("gupy.io"))
            return (SourceType.OfficialAts, host, 80, false);
        if (Has("linkedin."))
            return (SourceType.SocialIndexed, "linkedin.com", 25, true);
        if (Has("indeed.", "glassdoor.", "jobgether", "simplyhired", "ziprecruiter", "bebee", "catho.", "reddit."))
            return (SourceType.Aggregator, host, 35, true);
        if (Has("programathor", "geekhunter", "coodesh", "remotar", "trampos", "gupy"))
            return (SourceType.JobBoard, host, 70, false);
        return (SourceType.SearchResult, host, 50, false);
    }

    private static string TryHost(string url)
    {
        try { return new Uri(url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase); }
        catch { return url; }
    }

    private static SearchCampaignPriority ParsePriority(string? p) =>
        Enum.TryParse<SearchCampaignPriority>(p, ignoreCase: true, out var v) ? v : SearchCampaignPriority.Medium;

    private static SearchCampaignResponse ToResponse(SearchCampaign c) => new(
        c.Id, c.Name, c.Description, c.Status.ToString(), c.Priority.ToString(),
        c.BaseKeywords, c.TargetSources, c.ExcludedDomains, c.DailyQueryBudget, c.CreatedAtUtc, c.LastRunAtUtc);

    private static RawJobCandidateResponse ToResponse(RawJobCandidate r) => new(
        r.Id, r.Title, r.Snippet, r.DiscoveredUrl, r.SourceProvider, r.SourceName, r.SourceType.ToString(),
        r.RealCompanyName, r.OriginalJobUrl, r.Location, r.WorkMode, r.Language, r.DiscoveredAtUtc,
        r.PublishedAtUtc, r.Status.ToString(), r.SourceConfidenceScore, r.PreliminaryFitScore,
        r.RequiresManualValidation, r.SearchCampaignId, r.Query);

    private sealed class RunStats
    {
        public int Queries, Results, New, Duplicates;
        public readonly HashSet<string> SeenUrls = new(StringComparer.OrdinalIgnoreCase);
    }
}
