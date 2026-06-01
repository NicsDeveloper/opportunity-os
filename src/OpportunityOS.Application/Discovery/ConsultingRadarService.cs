using Microsoft.Extensions.Logging;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Discovery;

public interface IConsultingRadarService
{
    Task<ConsultingDiscoverResponse> DiscoverAsync(ConsultingDiscoverRequest req, CancellationToken ct);
    Task<IReadOnlyList<ConsultingCandidateResponse>> GetCandidatesAsync(int take, CancellationToken ct);
    Task<ConsultingCandidateResponse?> GetCandidateAsync(Guid id, CancellationToken ct);
    Task<ConsultingPromotionResponse> PromoteAsync(Guid id, CancellationToken ct);
    Task<ConsultingPromotionResponse> PromoteBatchAsync(PromoteConsultingBatchRequest req, CancellationToken ct);
}

/// <summary>
/// Discovers IT consultancies / software houses automatically (Priority 3): runs consulting
/// queries via raw search, derives company candidates from result hosts, scores them with
/// explainable signals, dedups, and seeds a curated list. Promotes confident ones to Company.
/// No LLM.
/// </summary>
public sealed class ConsultingRadarService : IConsultingRadarService
{
    public static readonly string[] DiscoveryQueries =
    {
        "consultoria TI Brasil .NET vagas", "consultoria tecnologia Brasil C#",
        "outsourcing TI Brasil desenvolvedor .NET", "software house Brasil C#",
        "fábrica de software .NET Brasil", "consultoria transformação digital vagas .NET",
        "nearshore Brazil .NET developer",
        "consultoria .NET São Paulo", "consultoria .NET Rio de Janeiro", "consultoria .NET Belo Horizonte",
        "consultoria .NET Curitiba", "consultoria .NET Recife", "consultoria .NET Porto Alegre",
        "consultoria .NET Florianópolis", "consultoria .NET Campinas", "consultoria .NET Brasília",
    };

    public static readonly string[] Seeds =
    {
        "GFT", "Stefanini", "BRQ", "CI&T", "Compass UOL", "FCamara", "TIVIT", "Meta", "ilegra", "Zup",
        "DB1", "NTT Data", "Accenture", "Deloitte", "Capgemini", "IBM", "Thoughtworks", "BairesDev",
        "Encora", "Globant", "Invillia", "Objective", "Concrete", "Iteris", "Spread", "Sinqia",
        "Matera", "K2 Partnering", "Act Digital",
    };

    private static readonly string[] SkipHosts =
    {
        "linkedin.", "indeed.", "glassdoor.", "gupy.io", "greenhouse.io", "lever.co", "ashbyhq.com",
        "smartrecruiters.com", "jobgether", "google.", "facebook.", "instagram.", "youtube.", "wikipedia.",
        "programathor", "geekhunter", "coodesh", "reddit.", "catho.",
    };

    private readonly IEnumerable<IRawSearchProvider> _providers;
    private readonly IConsultingStore _store;
    private readonly IQueryBudgetManager _budget;
    private readonly ILogger<ConsultingRadarService> _logger;

    public ConsultingRadarService(
        IEnumerable<IRawSearchProvider> providers, IConsultingStore store,
        IQueryBudgetManager budget, ILogger<ConsultingRadarService> logger)
    {
        _providers = providers;
        _store = store;
        _budget = budget;
        _logger = logger;
    }

    public async Task<ConsultingDiscoverResponse> DiscoverAsync(ConsultingDiscoverRequest req, CancellationToken ct)
    {
        var run = ExecutionRun.Start("ConsultingRadar");
        await _store.AddExecutionRunAsync(run, ct);
        int queries = 0, found = 0, dups = 0;
        var seenHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (req.IncludeSeeds ?? true)
            found += await SeedAsync(ct);

        var max = Math.Clamp(req.MaxQueries ?? DiscoveryQueries.Length, 1, DiscoveryQueries.Length);
        foreach (var query in DiscoveryQueries.Take(max))
        {
            foreach (var provider in _providers.Where(p => p.IsAvailable))
            {
                if (!await _budget.CanExecuteAsync(provider.ProviderName, ct)) continue;
                queries++;
                try
                {
                    var results = await provider.SearchAsync(query, 10, ct);
                    await _budget.RecordExecutionAsync(provider.ProviderName, 1, ct);
                    foreach (var r in results)
                    {
                        var host = HostOf(r.Url);
                        if (host is null || SkipHosts.Any(s => host.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;
                        var name = CompanyFromHost(host);
                        var (score, signals) = ConsultingSignals.Evaluate($"{r.Title} {r.Snippet}", hasWebsite: true, multipleSources: !seenHosts.Add(host));

                        var existing = await _store.FindCandidateByNameAsync(name, ct);
                        if (existing is not null) { existing.Reinforce(signals, 5); dups++; continue; }
                        await _store.AddCandidateAsync(new ConsultingCompanyCandidate(
                            name, $"https://{host}", provider.ProviderName, signals, score), ct);
                        found++;
                    }
                    run.RecordSuccess();
                }
                catch (Exception ex) { run.RecordFailure($"{provider.ProviderName}: {ex.Message}"); _logger.LogWarning(ex, "Consulting query failed: {Q}", query); }
            }
            await _store.SaveChangesAsync(ct);
        }

        run.Complete();
        await _store.SaveChangesAsync(ct);
        return new ConsultingDiscoverResponse(run.Id, run.Status.ToString(), queries, found, dups);
    }

    private async Task<int> SeedAsync(CancellationToken ct)
    {
        int added = 0;
        foreach (var name in Seeds)
        {
            if (await _store.FindCandidateByNameAsync(name, ct) is not null) continue;
            await _store.AddCandidateAsync(new ConsultingCompanyCandidate(
                name, null, "seed", new[] { "+60 seed (consultoria conhecida)" }, 60), ct);
            added++;
        }
        await _store.SaveChangesAsync(ct);
        return added;
    }

    public async Task<IReadOnlyList<ConsultingCandidateResponse>> GetCandidatesAsync(int take, CancellationToken ct) =>
        (await _store.GetCandidatesAsync(Math.Clamp(take, 1, 500), ct)).Select(ToResponse).ToList();

    public async Task<ConsultingCandidateResponse?> GetCandidateAsync(Guid id, CancellationToken ct)
    {
        var c = await _store.GetCandidateAsync(id, ct);
        return c is null ? null : ToResponse(c);
    }

    public async Task<ConsultingPromotionResponse> PromoteAsync(Guid id, CancellationToken ct)
    {
        var c = await _store.GetCandidateAsync(id, ct);
        if (c is null) return new ConsultingPromotionResponse(0, 0, 0);
        var ok = await PromoteOneAsync(c, ct);
        await _store.SaveChangesAsync(ct);
        return new ConsultingPromotionResponse(1, ok ? 1 : 0, ok ? 0 : 1);
    }

    public async Task<ConsultingPromotionResponse> PromoteBatchAsync(PromoteConsultingBatchRequest req, CancellationToken ct)
    {
        var minConf = req.MinConfidence ?? 70;        // spec: não promover automaticamente < 70
        var candidates = await _store.GetPromotableAsync(minConf, Math.Clamp(req.Max ?? 100, 1, 500), ct);
        int promoted = 0, skipped = 0;
        foreach (var c in candidates)
        {
            if (await PromoteOneAsync(c, ct)) promoted++; else skipped++;
            await _store.SaveChangesAsync(ct);
        }
        return new ConsultingPromotionResponse(candidates.Count, promoted, skipped);
    }

    private async Task<bool> PromoteOneAsync(ConsultingCompanyCandidate c, CancellationToken ct)
    {
        if (c.Status == ConsultingCompanyCandidateStatus.PromotedToCompany) return false;
        if (c.ConsultingConfidenceScore < 70) { return false; }            // safety gate
        if (await _store.CompanyExistsByNameAsync(c.Name, ct)) { c.MarkPromoted(); return false; }

        var priority = c.ConsultingConfidenceScore >= 80 ? CompanyPriority.High
            : c.ConsultingConfidenceScore >= 60 ? CompanyPriority.Medium : CompanyPriority.Low;
        var company = new Company(c.Name, c.WebsiteUrl, null, c.LinkedInCompanyUrl, "IT Consulting", "Brazil",
            priority, CompanySource.SearchDiscovery, new[] { "consulting", "outsourcing", "software-house", "dotnet-priority" });
        await _store.AddCompanyAsync(company, ct);
        c.MarkPromoted();
        return true;
    }

    private static ConsultingCandidateResponse ToResponse(ConsultingCompanyCandidate c) => new(
        c.Id, c.Name, c.WebsiteUrl, c.LinkedInCompanyUrl, c.Country, c.Source, c.Signals,
        c.ConsultingConfidenceScore, c.Status.ToString(), c.CreatedAtUtc, c.PromotedAtUtc);

    private static string? HostOf(string url) { try { return new Uri(url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase); } catch { return null; } }

    private static string CompanyFromHost(string host)
    {
        var label = host.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? host;
        return label.Length == 0 ? host : char.ToUpperInvariant(label[0]) + label[1..];
    }
}
