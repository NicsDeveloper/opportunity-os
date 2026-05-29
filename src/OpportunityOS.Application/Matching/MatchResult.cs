using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Matching;

/// <summary>
/// Pure scoring output of the heuristic match engine. The API maps this onto
/// the <c>OpportunityMatch</c> entity for persistence.
/// </summary>
public sealed record MatchResult(
    int OverallScore,
    int TechnicalScore,
    int DomainScore,
    int SeniorityScore,
    int LocationScore,
    int LanguageScore,
    MatchRecommendation Recommendation,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> MissingRequirements,
    string Rationale);
