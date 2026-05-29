using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

public sealed class JobUnderstandingService : AiServiceBase, IJobUnderstandingService
{
    private readonly IJobNormalizer _normalizer;

    public JobUnderstandingService(
        ILlmProvider llm,
        IPromptExecutionLogStore audit,
        IJobNormalizer normalizer,
        ILogger<JobUnderstandingService> logger)
        : base(llm, audit, logger) => _normalizer = normalizer;

    protected override string ServiceName => "JobUnderstanding";

    public Task<JobAnalysisResult> AnalyzeAsync(JobPosting job, CancellationToken ct) =>
        ExecuteAsync(Prompts.JobAnalysis(job), () => HeuristicFallback(job), job.Id, null, ct);

    /// <summary>Deterministic fallback: derive analysis from the heuristic normalizer.</summary>
    private JobAnalysisResult HeuristicFallback(JobPosting job)
    {
        var n = _normalizer.Normalize(job);
        var summary = job.DescriptionText.Length > 280 ? job.DescriptionText[..280] + "…" : job.DescriptionText;
        return new JobAnalysisResult(
            RequiredSkills: n.Skills.ToList(),
            NiceToHaveSkills: new(),
            Domains: n.Domains.ToList(),
            Seniority: n.Seniority ?? "Unknown",
            WorkMode: n.WorkMode ?? "Unknown",
            Language: n.Language ?? job.Language ?? "Unknown",
            Responsibilities: new(),
            Risks: new(),
            Summary: summary);
    }
}
