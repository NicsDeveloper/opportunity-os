using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

/// <summary>Outcome of inspecting a company's site for its ATS.</summary>
public sealed record AtsDetectionResult(
    bool Detected,
    string? Ats,            // "Greenhouse" | "Lever" | "Gupy" | "Workday"
    string? BoardUrl,       // canonical board/careers URL to store on the company
    string? Token,          // board token / handle / slug / tenant
    string? CareersPageUrl, // the careers page we found, if any
    bool ProviderSupported);// whether we already have a fetch provider for this ATS

/// <summary>
/// Crawls a company's public website to find its careers page and detect which
/// ATS it uses (Greenhouse, Lever, Gupy, Workday). Public pages only; respects
/// timeouts/backoff. Implemented in Infrastructure (needs HTTP).
/// </summary>
public interface IAtsDetector
{
    Task<AtsDetectionResult> DetectAsync(Company company, CancellationToken cancellationToken);
}
