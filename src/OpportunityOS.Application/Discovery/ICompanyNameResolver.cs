using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Discovery;

public sealed record CompanyResolutionResult(
    string? RealCompanyName,
    string SourceName,
    SourceType SourceType,
    int Confidence,
    string? Reason);

/// <summary>
/// Separates the REAL hiring company from the SOURCE where the posting was found. An
/// aggregator host (Jobgether, Indeed, LinkedIn...) is never assumed to be the company.
/// Heuristic-first (title/snippet/host); LLM fallback is intentionally deferred to keep cost low.
/// </summary>
public interface ICompanyNameResolver
{
    Task<CompanyResolutionResult> ResolveAsync(RawJobCandidate candidate, CancellationToken ct);

    /// <summary>Synchronous heuristic resolution from raw fields (used before a candidate exists).</summary>
    CompanyResolutionResult Resolve(string? title, string url, string sourceName, SourceType sourceType);
}
