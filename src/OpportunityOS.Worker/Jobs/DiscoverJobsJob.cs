using Hangfire;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Worker.Jobs;

/// <summary>Recurring job that runs job discovery across all companies.</summary>
public sealed class DiscoverJobsJob
{
    private readonly IJobDiscoveryService _discovery;
    private readonly ILogger<DiscoverJobsJob> _logger;

    public DiscoverJobsJob(IJobDiscoveryService discovery, ILogger<DiscoverJobsJob> logger)
    {
        _discovery = discovery;
        _logger = logger;
    }

    [JobDisplayName("Discover jobs (all companies)")]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("DiscoverJobsJob starting");
        var result = await _discovery.DiscoverAsync(null, ct);
        _logger.LogInformation(
            "DiscoverJobsJob finished {Status}: companies={Companies} new={New} updated={Updated} errors={Errors}",
            result.Status, result.CompaniesProcessed, result.JobsDiscovered, result.JobsUpdated, result.Errors);
    }
}
