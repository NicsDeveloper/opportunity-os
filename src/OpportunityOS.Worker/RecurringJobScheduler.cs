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

        // Continuous discovery: keep finding jobs while the system runs (rate-friendly cadence).
        var continuousCron = _config.GetValue<string>("Jobs:ContinuousDiscoveryCron") ?? "*/15 * * * *";
        _recurring.AddOrUpdate<DiscoverJobsJob>(
            "continuous-discovery",
            job => job.RunAsync(CancellationToken.None),
            continuousCron);
        _logger.LogInformation("Scheduled recurring job 'continuous-discovery' with cron {Cron}", continuousCron);

        // Keyword search (Gupy + open-web Google) a few times a day, to spread the Google quota.
        var searchCron = _config.GetValue<string>("Jobs:SearchCron") ?? "0 */3 * * *";
        _recurring.AddOrUpdate<SearchJobsJob>(
            "search-jobs",
            job => job.RunAsync(CancellationToken.None),
            searchCron);
        _logger.LogInformation("Scheduled recurring job 'search-jobs' with cron {Cron}", searchCron);

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
