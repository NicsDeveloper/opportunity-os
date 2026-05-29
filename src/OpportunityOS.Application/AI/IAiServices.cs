using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

/// <summary>1. Interprets a job description and extracts structured requirements.</summary>
public interface IJobUnderstandingService
{
    Task<JobAnalysisResult> AnalyzeAsync(JobPosting job, CancellationToken cancellationToken);
}

/// <summary>2. Compares a job with the candidate profile and produces a fit (OpportunityMatch).</summary>
public interface ICandidateFitAnalysisService
{
    Task<OpportunityMatch> AnalyzeFitAsync(
        CandidateProfile profile,
        JobPosting job,
        JobAnalysisResult jobAnalysis,
        CancellationToken cancellationToken);
}

/// <summary>3. Generates outreach drafts (LinkedIn/email/cover letter/follow-up).</summary>
public interface IOutreachDraftService
{
    Task<GeneratedOutreachResult> GenerateAsync(
        CandidateProfile profile,
        JobPosting job,
        OpportunityMatch match,
        CancellationToken cancellationToken);
}

/// <summary>4. Suggests CV adjustments for a specific job. Advisory only.</summary>
public interface ICvTailoringSuggestionService
{
    Task<CvTailoringSuggestion> SuggestAsync(
        CandidateProfile profile,
        JobPosting job,
        OpportunityMatch match,
        CancellationToken cancellationToken);
}

/// <summary>5. Analyzes patterns across many jobs to produce career insights.</summary>
public interface ICareerInsightService
{
    Task<CareerInsightReport> GenerateInsightsAsync(
        CandidateProfile profile,
        IReadOnlyCollection<JobPosting> analyzedJobs,
        CancellationToken cancellationToken);
}

/// <summary>Audit sink for LLM executions (promptVersion, modelName, rawResponse...).</summary>
public interface IPromptExecutionLogStore
{
    Task SaveAsync(PromptExecutionLog log, CancellationToken cancellationToken);
}
