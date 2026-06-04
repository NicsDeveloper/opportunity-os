using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Inputs for the discovery ranking. FitScore = how well the job matches the candidate
/// (the OpportunityMatch score). The other signals decide how much it deserves the top.
/// </summary>
public sealed record DiscoveryRankInput(
    int FitScore,
    int SourceConfidenceScore,
    DateTime EffectiveDateUtc,
    CompanyPriority CompanyPriority,
    int UserFeedbackBoost = 50);

/// <summary>
/// Two separate scores (Priority 15):
///   FitScore       — how well the job matches the candidate (relevance).
///   DiscoveryRank  — how much this job deserves to surface at the top.
/// DiscoveryRank = Fit*0.50 + SourceConfidence*0.20 + Freshness*0.15 + CompanyPriority*0.10 + Feedback*0.05.
/// </summary>
public interface IDiscoveryRankService
{
    int ComputeDiscoveryRank(DiscoveryRankInput input);
    int FreshnessScore(DateTime effectiveUtc);
    int CompanyPriorityScore(CompanyPriority priority);

    /// <summary>Boost (0–100, neutro=50) derivado do feedback do usuário sobre a vaga.</summary>
    int FeedbackBoost(IEnumerable<UserFeedbackType> feedback);
}
