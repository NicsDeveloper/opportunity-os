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

    public int FeedbackBoost(IEnumerable<UserFeedbackType> feedback)
    {
        var boost = 50; // neutral
        foreach (var f in feedback)
            boost += f switch
            {
                UserFeedbackType.Applied => 30,
                UserFeedbackType.Relevant => 25,
                UserFeedbackType.ContactedRecruiter => 20,
                UserFeedbackType.InterestingCompany => 15,
                UserFeedbackType.Irrelevant => -40,
                UserFeedbackType.HideSimilar => -30,
                UserFeedbackType.BadScore => -15,
                UserFeedbackType.BadCompanyDetection => -10,
                _ => 0,
            };
        return Clamp(boost);
    }

    private static int Clamp(int v) => Math.Clamp(v, 0, 100);
}
