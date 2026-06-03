using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Matching;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.AI;

public sealed class CandidateFitAnalysisService : AiServiceBase, ICandidateFitAnalysisService
{
    private readonly IMatchEngine _matchEngine;

    public CandidateFitAnalysisService(
        ILlmProvider llm,
        IPromptExecutionLogStore audit,
        IMatchEngine matchEngine,
        ILogger<CandidateFitAnalysisService> logger)
        : base(llm, audit, logger) => _matchEngine = matchEngine;

    protected override string ServiceName => "CandidateFitAnalysis";

    public async Task<OpportunityMatch> AnalyzeFitAsync(
        CandidateProfile profile, JobPosting job, JobAnalysisResult jobAnalysis, CancellationToken ct)
    {
        var dto = await ExecuteAsync(
            Prompts.Fit(profile, jobAnalysis),
            () => HeuristicFallback(profile, job),
            job.Id, profile.Id, ct);

        return MapToMatch(dto, job.Id, profile.Id);
    }

    private FitDto HeuristicFallback(CandidateProfile profile, JobPosting job)
    {
        var r = _matchEngine.Evaluate(profile, job);
        return new FitDto
        {
            TechnicalScore = r.TechnicalScore,
            DomainScore = r.DomainScore,
            SeniorityScore = r.SeniorityScore,
            LocationScore = r.LocationScore,
            LanguageScore = r.LanguageScore,
            OverallScore = r.OverallScore,
            Recommendation = r.Recommendation.ToString(),
            Rationale = r.Rationale,
            Strengths = r.Strengths.ToList(),
            Risks = r.Risks.ToList(),
            MissingRequirements = r.MissingRequirements.ToList()
        };
    }

    private static OpportunityMatch MapToMatch(FitDto d, Guid jobId, Guid profileId)
    {
        int Clamp(int v) => Math.Clamp(v, 0, 100);
        var overall = d.OverallScore > 0
            ? Clamp(d.OverallScore)
            : Clamp((int)Math.Round(
                d.TechnicalScore * 0.35 + d.DomainScore * 0.25 + d.SeniorityScore * 0.15 +
                d.LocationScore * 0.15 + d.LanguageScore * 0.10));

        var recommendation = Enum.TryParse<MatchRecommendation>(d.Recommendation, ignoreCase: true, out var r)
            ? r
            : overall switch
            {
                >= 90 => MatchRecommendation.Strategic,
                >= 75 => MatchRecommendation.Prioritize,
                >= 60 => MatchRecommendation.Apply,
                >= 40 => MatchRecommendation.SaveForLater,
                _ => MatchRecommendation.Ignore
            };

        return new OpportunityMatch(
            jobId, profileId, overall, Clamp(d.TechnicalScore), Clamp(d.DomainScore),
            Clamp(d.SeniorityScore), Clamp(d.LocationScore), Clamp(d.LanguageScore),
            recommendation, d.Strengths ?? new(), d.Risks ?? new(), d.MissingRequirements ?? new(),
            string.IsNullOrWhiteSpace(d.Rationale) ? $"Score {overall}/100" : d.Rationale, "llm-fit-v1");
    }

    /// <summary>Wire shape for the fit LLM response.</summary>
    private sealed class FitDto
    {
        public int TechnicalScore { get; set; }
        public int DomainScore { get; set; }
        public int SeniorityScore { get; set; }
        public int LocationScore { get; set; }
        public int LanguageScore { get; set; }
        public int OverallScore { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public List<string>? Strengths { get; set; }
        public List<string>? Risks { get; set; }
        public List<string>? MissingRequirements { get; set; }
    }
}
