using Hangfire;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Worker.Jobs;

/// <summary>
/// Periodic keyword search across search providers (Gupy + open-web Google) for the
/// candidate's core terms. Runs a few times a day so the Google quota lasts.
/// </summary>
public sealed class SearchJobsJob
{
    private static readonly string[] Keywords =
    {
        "desenvolvedor .net", "desenvolvedor backend c#", "engenheiro de software .net",
        "programador c# pleno", "vaga .net remoto", "desenvolvedor .net fintech",
        "arquiteto .net", "desenvolvedor c# sênior",
    };

    private readonly IJobDiscoveryService _discovery;
    private readonly ILogger<SearchJobsJob> _logger;

    public SearchJobsJob(IJobDiscoveryService discovery, ILogger<SearchJobsJob> logger)
    {
        _discovery = discovery;
        _logger = logger;
    }

    [JobDisplayName("Keyword search (Gupy + web)")]
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        var r = await _discovery.SearchAsync(Keywords, ct);
        _logger.LogInformation("SearchJobsJob finished {Status}: new={New} updated={Updated} errors={Errors}",
            r.Status, r.JobsDiscovered, r.JobsUpdated, r.Errors);
    }
}
