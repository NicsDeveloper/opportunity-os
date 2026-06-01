using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Computes DiscoveryRank from a weighted blend of relevance + trust + freshness +
/// company priority + user feedback. Pure/stateless.
/// </summary>
public sealed class DiscoveryRankService : IDiscoveryRankService
{
    public int ComputeDiscoveryRank(DiscoveryRankInput i)
    {
        var rank =
            Clamp(i.FitScore) * 0.50 +
            Clamp(i.SourceConfidenceScore) * 0.20 +
            FreshnessScore(i.EffectiveDateUtc) * 0.15 +
            CompanyPriorityScore(i.CompanyPriority) * 0.10 +
            Clamp(i.UserFeedbackBoost) * 0.05;
        return (int)Math.Round(rank, MidpointRounding.AwayFromZero);
    }

    public int FreshnessScore(DateTime effectiveUtc)
    {
        var days = (DateTime.UtcNow - effectiveUtc).TotalDays;
        return days switch
        {
            <= 2 => 100,
            <= 7 => 90,
            <= 30 => 70,
            <= 90 => 50,
            <= 180 => 30,
            _ => 10,
        };
    }

    public int CompanyPriorityScore(CompanyPriority priority) => priority switch
    {
        CompanyPriority.Strategic => 100,
        CompanyPriority.High => 75,
        CompanyPriority.Medium => 50,
        _ => 25,
    };

    private static int Clamp(int v) => Math.Clamp(v, 0, 100);
}
