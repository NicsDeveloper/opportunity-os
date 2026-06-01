using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

public sealed record OriginalJobResolutionResult(
    bool Found,
    string? OriginalUrl,
    string? CompanyName,
    string? AtsProvider,
    int Confidence,
    string? Reason);

/// <summary>
/// Turns an aggregator/snippet candidate into an actionable, verified posting (Priority 7):
/// searches for the same role on the company's official ATS/site. If not found, the candidate
/// is kept (AggregatorOnly), never discarded. Uses raw search (budget-guarded), no LLM.
/// </summary>
public interface IOriginalJobResolver
{
    Task<OriginalJobResolutionResult> ResolveAsync(RawJobCandidate candidate, CancellationToken ct);
}
