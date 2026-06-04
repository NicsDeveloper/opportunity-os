using Hangfire;
using OpportunityOS.Application.Digest;

namespace OpportunityOS.Worker.Jobs;

/// <summary>Recurring job that builds and sends the daily opportunities digest.</summary>
public sealed class SendDailyDigestJob
{
    private readonly IEmailDigestService _digest;
    private readonly ILogger<SendDailyDigestJob> _logger;

    public SendDailyDigestJob(IEmailDigestService digest, ILogger<SendDailyDigestJob> logger)
    {
        _digest = digest;
        _logger = logger;
    }

    [JobDisplayName("Send daily digest")]
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        // One digest per candidate profile (each uses its own MinimumScoreToShow).
        var results = await _digest.SendAllAsync(ct);
        _logger.LogInformation(
            "SendDailyDigestJob finished: profiles={Profiles} sent={Sent} totalItems={Items}",
            results.Count, results.Count(r => r.Sent), results.Sum(r => r.ItemCount));
    }
}
