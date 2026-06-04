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

        // Expire dead links so the fresh feed never shows closed/404 vacancies.
        var validateCron = _config.GetValue<string>("Jobs:ValidateLinksCron") ?? "30 */6 * * *";
        _recurring.AddOrUpdate<ValidateLinksJob>(
            "validate-links",
            job => job.RunAsync(CancellationToken.None),
            validateCron);
        _logger.LogInformation("Scheduled recurring job 'validate-links' with cron {Cron}", validateCron);

        // Firehose: broad discovery fills the candidate pool by itself (budget-guarded).
        var firehoseCron = _config.GetValue<string>("Jobs:FirehoseCron") ?? "0 */4 * * *";
        _recurring.AddOrUpdate<FirehoseSweepJob>(
            "firehose-sweep", job => job.RunAsync(CancellationToken.None), firehoseCron);
        _logger.LogInformation("Scheduled recurring job 'firehose-sweep' with cron {Cron}", firehoseCron);

        // Promote promising candidates (with confirmed company) into the qualified feed.
        var promotionCron = _config.GetValue<string>("Jobs:PromotionCron") ?? "30 */4 * * *";
        _recurring.AddOrUpdate<PromoteCandidatesJob>(
            "promote-candidates", job => job.RunAsync(CancellationToken.None), promotionCron);
        _logger.LogInformation("Scheduled recurring job 'promote-candidates' with cron {Cron}", promotionCron);

        // Cost-heavy sweeps: opt-in via feature flags (off by default).
        if (_config.GetValue("FeatureFlags:EnableBacenSweepJob", false))
        {
            var bacenCron = _config.GetValue<string>("Jobs:BacenSweepCron") ?? "0 6 * * 1";
            _recurring.AddOrUpdate<BacenFinancialSweepJob>(
                "bacen-financial-sweep", job => job.RunAsync(CancellationToken.None), bacenCron);
            _logger.LogInformation("Scheduled recurring job 'bacen-financial-sweep' with cron {Cron}", bacenCron);
        }
        else _recurring.RemoveIfExists("bacen-financial-sweep");

        if (_config.GetValue("FeatureFlags:EnableConsultingRadarJob", false))
        {
            var consultingCron = _config.GetValue<string>("Jobs:ConsultingRadarCron") ?? "0 7 * * 2";
            _recurring.AddOrUpdate<ConsultingRadarJob>(
                "consulting-radar", job => job.RunAsync(CancellationToken.None), consultingCron);
            _logger.LogInformation("Scheduled recurring job 'consulting-radar' with cron {Cron}", consultingCron);
        }
        else _recurring.RemoveIfExists("consulting-radar");

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
