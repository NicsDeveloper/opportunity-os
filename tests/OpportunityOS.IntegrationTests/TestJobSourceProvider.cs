using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.IntegrationTests;

/// <summary>
/// Deterministic provider for integration tests: returns one fixed posting for
/// any company, so /api/jobs/discover never touches the network.
/// </summary>
public sealed class TestJobSourceProvider : IJobSourceProvider
{
    public const string ExternalId = "it-1";

    public string ProviderName => "Test";
    public bool CanHandle(Company company) => true;

    public Task<IReadOnlyCollection<DiscoveredJobDto>> DiscoverJobsAsync(Company company, CancellationToken ct)
    {
        IReadOnlyCollection<DiscoveredJobDto> jobs = new[]
        {
            new DiscoveredJobDto(
                ExternalId, "Senior Backend Engineer (.NET / Payments)", company.Name,
                "Remote - Brazil", "Engineering", null,
                "Build payments and PIX systems with .NET, C#, Kafka and AWS.",
                "https://example.com/it-1", "Test", null, DateTime.UtcNow, "en")
        };
        return Task.FromResult(jobs);
    }
}
