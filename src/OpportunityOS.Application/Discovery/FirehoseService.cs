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
    private readonly IEnumerable<IRawSearchProvider> _providers;
    private readonly QueryExpansionService _expansion;
    private readonly IFirehoseStore _store;
    private readonly IQueryBudgetManager _budget;
    private readonly DiscoveryBudgetOptions _budgetOptions;
    private readonly ISourceClassifierService _classifier;
    private readonly ILogger<FirehoseService> _logger;

    public FirehoseService(
        IEnumerable<IRawSearchProvider> providers, QueryExpansionService expansion,
        IFirehoseStore store, IQueryBudgetManager budget, DiscoveryBudgetOptions budgetOptions,
        ISourceClassifierService classifier, ILogger<FirehoseService> logger)
    {
        _providers = providers;
        _expansion = expansion;
        _store = store;
        _budget = budget;
        _budgetOptions = budgetOptions;
        _classifier = classifier;
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
        var take = Math.Clamp(req.Take is > 0 ? req.Take!.Value : 20, 1, _budgetOptions.SerperMaxResultsPerQuery);
        var save = req.SaveRawCandidates ?? true;
        var run = ExecutionRun.Start("FirehoseQuickSearch");
        await _store.AddExecutionRunAsync(run, ct);

        var stats = new RunStats();
        await RunQueryAsync(Guid.Empty, req.Query, take, save, run, stats, ct);

        run.Complete();
        await _store.SaveChangesAsync(ct);
        return new FirehoseRunResponse(run.Id, Status(run, stats), null,
            stats.Queries, stats.Results, stats.New, stats.Duplicates, run.ItemsFailed);
    }

    public async Task<FirehoseRunResponse> AggressiveSearchAsync(AggressiveSearchRequest req, CancellationToken ct)
    {
        var maxQueries = Math.Clamp(req.MaxQueries is > 0 ? req.MaxQueries!.Value : _budgetOptions.AggressiveSearchMaxQueries,
            1, _budgetOptions.AggressiveSearchMaxQueries);
        var maxResults = Math.Clamp(req.MaxResultsPerQuery is > 0 ? req.MaxResultsPerQuery!.Value : _budgetOptions.SerperMaxResultsPerQuery,
            1, _budgetOptions.SerperMaxResultsPerQuery);
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
            if (stats.BudgetExhausted) // all providers out of daily budget -> stop cleanly
            {
                _logger.LogInformation("FirehoseAggressive stopped early: daily query budget exhausted.");
                break;
            }
        }

        campaign?.MarkRun();
        run.Complete();
        await _store.SaveChangesAsync(ct);
        _logger.LogInformation("FirehoseAggressive done: queries={Q} results={R} new={N} dup={D} errors={E} budgetHit={B}",
            stats.Queries, stats.Results, stats.New, stats.Duplicates, run.ItemsFailed, stats.BudgetExhausted);
        return new FirehoseRunResponse(run.Id, Status(run, stats), campaign?.Id,
            stats.Queries, stats.Results, stats.New, stats.Duplicates, run.ItemsFailed);
    }

    private static string Status(ExecutionRun run, RunStats stats) =>
        stats.BudgetExhausted && run.Status == Domain.Enums.ExecutionRunStatus.Succeeded
            ? "CompletedWithBudgetLimit"
            : run.Status.ToString();

    public async Task<IReadOnlyList<RawJobCandidateResponse>> GetRawCandidatesAsync(int take, CancellationToken ct) =>
        (await _store.GetRawCandidatesAsync(Math.Clamp(take, 1, 1000), ct)).Select(ToResponse).ToList();

    private async Task RunQueryAsync(
        Guid campaignId, string query, int maxResults, bool save, ExecutionRun run, RunStats stats, CancellationToken ct)
    {
        var available = _providers.Where(p => p.IsAvailable).ToList();
        var blocked = 0;
        foreach (var provider in available)
        {
            // Budget guard: skip a provider that's out of daily budget (never break silently).
            if (!await _budget.CanExecuteAsync(provider.ProviderName, ct)) { blocked++; continue; }

            stats.Queries++;
            var exec = SearchQueryExecution.Start(campaignId, query, provider.ProviderName);
            await _store.AddQueryExecutionAsync(exec, ct);
            try
            {
                var results = await provider.SearchAsync(query, maxResults, ct);
                // Cost ≈ number of search requests (Serper paginates in blocks of 10).
                await _budget.RecordExecutionAsync(provider.ProviderName, Math.Max(1, (int)Math.Ceiling(maxResults / 10.0)), ct);
                int newCount = 0, dupCount = 0;
                foreach (var r in results)
                {
                    stats.Results++;
                    if (string.IsNullOrWhiteSpace(r.Url)) continue;
                    if (!stats.SeenUrls.Add(r.Url)) { dupCount++; stats.Duplicates++; continue; }
                    if (!save) continue;
                    if (await _store.RawCandidateExistsByUrlAsync(r.Url, ct)) { dupCount++; stats.Duplicates++; continue; }

                    var cls = _classifier.Classify(r.Url, r.Title, r.Snippet);
                    var candidate = new RawJobCandidate(
                        r.Title, r.Url, provider.ProviderName, cls.SourceName, cls.SourceType,
                        cls.SourceConfidenceScore, cls.RequiresManualValidation, r.Snippet,
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

        // Every available provider was out of budget for this query -> signal a clean stop.
        if (available.Count > 0 && blocked == available.Count) stats.BudgetExhausted = true;
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
        public bool BudgetExhausted;
        public readonly HashSet<string> SeenUrls = new(StringComparer.OrdinalIgnoreCase);
    }
}
