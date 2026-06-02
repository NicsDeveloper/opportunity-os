using Hangfire;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;

namespace OpportunityOS.Worker.Jobs;

/// <summary>
/// Periodic broad ("aggressive") discovery so the Firehose fills itself without manual action.
/// Budget-guarded by the QueryBudgetManager — stops cleanly when the daily quota is spent.
/// </summary>
public sealed class FirehoseSweepJob
{
    private readonly IFirehoseService _firehose;
    private readonly IConfiguration _config;
    private readonly ILogger<FirehoseSweepJob> _logger;

    public FirehoseSweepJob(IFirehoseService firehose, IConfiguration config, ILogger<FirehoseSweepJob> logger)
    {
        _firehose = firehose;
        _config = config;
        _logger = logger;
    }

    [JobDisplayName("Busca ampla (Firehose)")]
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        var maxQueries = _config.GetValue("Jobs:FirehoseMaxQueries", 60);
        var r = await _firehose.AggressiveSearchAsync(
            new AggressiveSearchRequest(null, maxQueries, null, SaveRawCandidates: true, PromoteAutomatically: false), ct);
        _logger.LogInformation("FirehoseSweepJob {Status}: queries={Q} results={R} new={N} dup={D} errors={E}",
            r.Status, r.QueriesExecuted, r.ResultsCount, r.NewCandidates, r.Duplicates, r.Errors);
    }
}
