using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// Result of evaluating compatibility between a job posting and the candidate.
/// </summary>
public sealed class OpportunityMatch
{
    public Guid Id { get; private set; }
    public Guid JobPostingId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public int OverallScore { get; private set; }
    public int TechnicalScore { get; private set; }
    public int DomainScore { get; private set; }
    public int SeniorityScore { get; private set; }
    public int LocationScore { get; private set; }
    public int LanguageScore { get; private set; }
    public MatchRecommendation Recommendation { get; private set; }
    public List<string> Strengths { get; private set; } = new();
    public List<string> Risks { get; private set; } = new();
    public List<string> MissingRequirements { get; private set; } = new();
    public string Rationale { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private OpportunityMatch() { }

    public OpportunityMatch(
        Guid jobPostingId,
        Guid candidateProfileId,
        int overallScore,
        int technicalScore,
        int domainScore,
        int seniorityScore,
        int locationScore,
        int languageScore,
        MatchRecommendation recommendation,
        IEnumerable<string> strengths,
        IEnumerable<string> risks,
        IEnumerable<string> missingRequirements,
        string rationale)
    {
        Id = Guid.NewGuid();
        JobPostingId = jobPostingId;
        CandidateProfileId = candidateProfileId;
        OverallScore = overallScore;
        TechnicalScore = technicalScore;
        DomainScore = domainScore;
        SeniorityScore = seniorityScore;
        LocationScore = locationScore;
        LanguageScore = languageScore;
        Recommendation = recommendation;
        Strengths = strengths.ToList();
        Risks = risks.ToList();
        MissingRequirements = missingRequirements.ToList();
        Rationale = rationale;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
