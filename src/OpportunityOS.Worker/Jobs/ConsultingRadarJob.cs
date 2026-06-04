using Hangfire;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;

namespace OpportunityOS.Worker.Jobs;

/// <summary>
/// Optional periodic consulting-company discovery. Off by default (cost): enable with
/// FeatureFlags:EnableConsultingRadarJob. Budget-guarded.
/// </summary>
public sealed class ConsultingRadarJob
{
    private readonly IConsultingRadarService _radar;
    private readonly IConfiguration _config;
    private readonly ILogger<ConsultingRadarJob> _logger;

    public ConsultingRadarJob(IConsultingRadarService radar, IConfiguration config, ILogger<ConsultingRadarJob> logger)
    {
        _radar = radar;
        _config = config;
        _logger = logger;
    }

    [JobDisplayName("Procurar consultorias")]
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        var maxQueries = _config.GetValue("Jobs:ConsultingMaxQueries", 8);
        var r = await _radar.DiscoverAsync(new ConsultingDiscoverRequest(maxQueries, IncludeSeeds: true), ct);
        _logger.LogInformation("ConsultingRadarJob {Status}: queries={Q} found={F} duplicates={D}",
            r.Status, r.QueriesExecuted, r.CandidatesFound, r.Duplicates);
    }
}
