using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Finds the original posting for an aggregator candidate by searching the role + company and
/// preferring an official ATS / company-domain hit. Budget-guarded; no LLM.
/// </summary>
public sealed class OriginalJobResolver : IOriginalJobResolver
{
    private static readonly (string Host, string Ats)[] AtsHosts =
    {
        ("greenhouse.io", "Greenhouse"), ("lever.co", "Lever"), ("ashbyhq.com", "Ashby"),
        ("smartrecruiters.com", "SmartRecruiters"), ("gupy.io", "Gupy"), ("workdayjobs.com", "Workday"),
    };

    private readonly IEnumerable<IRawSearchProvider> _providers;
    private readonly IQueryBudgetManager _budget;
    private readonly ILogger<OriginalJobResolver> _logger;

    public OriginalJobResolver(IEnumerable<IRawSearchProvider> providers, IQueryBudgetManager budget, ILogger<OriginalJobResolver> logger)
    {
        _providers = providers;
        _budget = budget;
        _logger = logger;
    }

    public async Task<OriginalJobResolutionResult> ResolveAsync(RawJobCandidate candidate, CancellationToken ct)
    {
        // Already an official ATS link -> nothing to resolve.
        if (candidate.SourceType == SourceType.OfficialAts)
            return new(true, candidate.DiscoveredUrl, candidate.RealCompanyName, AtsOf(candidate.DiscoveredUrl), 100, "Já é um link de ATS oficial");

        var company = candidate.RealCompanyName;
        if (string.IsNullOrWhiteSpace(company))
            return new(false, null, null, null, 0, "Sem empresa confirmada — não dá para buscar o original");

        var provider = _providers.FirstOrDefault(p => p.IsAvailable);
        if (provider is null || !await _budget.CanExecuteAsync(provider.ProviderName, ct))
            return new(false, null, company, null, 0, "Sem provider/orçamento de busca disponível");

        try
        {
            var query = $"\"{Trim(candidate.Title, 80)}\" \"{company}\"";
            var results = await provider.SearchAsync(query, 10, ct);
            await _budget.RecordExecutionAsync(provider.ProviderName, 1, ct);

            foreach (var r in results)
            {
                var host = HostOf(r.Url);
                if (host is null) continue;
                var ats = AtsHosts.FirstOrDefault(a => host.Contains(a.Host, StringComparison.OrdinalIgnoreCase));
                if (ats.Host is not null)
                    return new(true, r.Url, company, ats.Ats, 90, $"Original encontrado no ATS {ats.Ats}");
            }

            // No ATS, but maybe the company's own domain appears.
            var token = company.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
            if (token is { Length: >= 3 })
                foreach (var r in results)
                {
                    var host = HostOf(r.Url);
                    if (host is not null && host.Contains(token, StringComparison.OrdinalIgnoreCase))
                        return new(true, r.Url, company, null, 70, "Provável página oficial da empresa");
                }

            return new(false, null, company, null, 0, "Original não encontrado — manter como agregador");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Original-job resolution failed for {Id}", candidate.Id);
            return new(false, null, company, null, 0, "Falha na busca");
        }
    }

    private static string? AtsOf(string url)
    {
        var host = HostOf(url);
        return host is null ? null : AtsHosts.FirstOrDefault(a => host.Contains(a.Host, StringComparison.OrdinalIgnoreCase)).Ats;
    }

    private static string? HostOf(string url) { try { return new Uri(url).Host; } catch { return null; } }
    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
