using Hangfire;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Worker.Jobs;

/// <summary>
/// Periodically HEAD-checks active postings and expires dead links (404/410) so the fresh
/// feed never shows obsolete/closed vacancies.
/// </summary>
public sealed class ValidateLinksJob
{
    private readonly IJobLinkValidator _validator;
    private readonly ILogger<ValidateLinksJob> _logger;

    public ValidateLinksJob(IJobLinkValidator validator, ILogger<ValidateLinksJob> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    [JobDisplayName("Validate job links")]
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        var r = await _validator.ValidateAsync(200, ct);
        _logger.LogInformation("ValidateLinksJob finished: checked={Checked} expired={Expired}", r.Checked, r.Expired);
    }
}
