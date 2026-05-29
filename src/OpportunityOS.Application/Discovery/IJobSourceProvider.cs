using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

/// <summary>A job posting as returned by an external source, before persistence.</summary>
public sealed record DiscoveredJobDto(
    string ExternalId,
    string Title,
    string CompanyName,
    string? Location,
    string? Department,
    string? DescriptionHtml,
    string? DescriptionText,
    string AbsoluteUrl,
    string SourceProvider,
    DateTime? PublishedAtUtc,
    DateTime? UpdatedAtUtc,
    string? Language);

/// <summary>
/// Discovers public job postings for a company from a specific ATS / source.
/// Implementations must only hit public endpoints (no logged-in scraping).
/// </summary>
public interface IJobSourceProvider
{
    string ProviderName { get; }

    /// <summary>Whether this provider can derive a feed for the given company.</summary>
    bool CanHandle(Company company);

    Task<IReadOnlyCollection<DiscoveredJobDto>> DiscoverJobsAsync(
        Company company,
        CancellationToken cancellationToken);
}
