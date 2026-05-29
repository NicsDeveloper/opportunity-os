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
        var result = await _digest.SendDailyDigestAsync(EmailDigestService.DefaultMinScore, ct);
        _logger.LogInformation(
            "SendDailyDigestJob finished: sent={Sent} items={Items} reason={Reason}",
            result.Sent, result.ItemCount, result.Reason);
    }
}
