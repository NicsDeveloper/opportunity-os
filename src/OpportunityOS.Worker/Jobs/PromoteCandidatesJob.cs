using Hangfire;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;

namespace OpportunityOS.Worker.Jobs;

/// <summary>
/// Periodically promotes promising raw candidates (with a confirmed company) into qualified
/// JobPostings, so the Action/Qualified feeds keep filling automatically. No LLM.
/// </summary>
public sealed class PromoteCandidatesJob
{
    private readonly IRawCandidatePromotionService _promotion;
    private readonly IConfiguration _config;
    private readonly ILogger<PromoteCandidatesJob> _logger;

    public PromoteCandidatesJob(IRawCandidatePromotionService promotion, IConfiguration config, ILogger<PromoteCandidatesJob> logger)
    {
        _promotion = promotion;
        _config = config;
        _logger = logger;
    }

    [JobDisplayName("Promover candidatos")]
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        var max = _config.GetValue("Jobs:PromotionMax", 50);
        var minConf = _config.GetValue("Jobs:PromotionMinConfidence", 50);
        var r = await _promotion.PromoteBatchAsync(new PromoteBatchRequest(max, minConf, RequireRealCompany: true), ct);
        _logger.LogInformation("PromoteCandidatesJob: considered={C} promoted={P} duplicates={D} skipped={S}",
            r.Considered, r.Promoted, r.Duplicates, r.Skipped);
    }
}
