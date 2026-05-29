namespace OpportunityOS.Application.Discovery;

/// <summary>
/// A keyword-based discovery source (cross-company), e.g. the Gupy public portal
/// API. Unlike <see cref="IJobSourceProvider"/> (per registered company), this
/// finds jobs by search terms; the company is derived from each result.
/// </summary>
public interface IJobSearchProvider
{
    string ProviderName { get; }

    Task<IReadOnlyCollection<DiscoveredJobDto>> SearchAsync(
        IReadOnlyCollection<string> keywords,
        CancellationToken cancellationToken);
}
