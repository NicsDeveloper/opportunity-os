using Hangfire;
using OpportunityOS.Worker.Jobs;

namespace OpportunityOS.Worker;

/// <summary>
/// Registers/refreshes the recurring jobs on startup using cron expressions from
/// configuration (Jobs:DailyDiscoveryCron). Kept idempotent via AddOrUpdate.
/// </summary>
public sealed class RecurringJobScheduler : IHostedService
{
    private readonly IRecurringJobManager _recurring;
    private readonly IConfiguration _config;
    private readonly ILogger<RecurringJobScheduler> _logger;

    public RecurringJobScheduler(
        IRecurringJobManager recurring, IConfiguration config, ILogger<RecurringJobScheduler> logger)
    {
        _recurring = recurring;
        _config = config;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var discoveryCron = _config.GetValue<string>("Jobs:DailyDiscoveryCron") ?? "0 8 * * *";
        _recurring.AddOrUpdate<DiscoverJobsJob>(
            "discover-jobs",
            job => job.RunAsync(CancellationToken.None),
            discoveryCron);
        _logger.LogInformation("Scheduled recurring job 'discover-jobs' with cron {Cron}", discoveryCron);

        var digestCron = _config.GetValue<string>("Jobs:DailyDigestCron") ?? "0 9 * * *";
        _recurring.AddOrUpdate<SendDailyDigestJob>(
            "send-daily-digest",
            job => job.RunAsync(CancellationToken.None),
            digestCron);
        _logger.LogInformation("Scheduled recurring job 'send-daily-digest' with cron {Cron}", digestCron);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
