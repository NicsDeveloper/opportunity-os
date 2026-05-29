using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

public sealed class CvTailoringSuggestionService : AiServiceBase, ICvTailoringSuggestionService
{
    public CvTailoringSuggestionService(
        ILlmProvider llm, IPromptExecutionLogStore audit, ILogger<CvTailoringSuggestionService> logger)
        : base(llm, audit, logger) { }

    protected override string ServiceName => "CvTailoringSuggestion";

    public Task<CvTailoringSuggestion> SuggestAsync(
        CandidateProfile profile, JobPosting job, OpportunityMatch match, CancellationToken ct) =>
        ExecuteAsync(
            Prompts.CvTailoring(profile, job, match),
            () => HeuristicFallback(profile, job),
            job.Id, profile.Id, ct);

    /// <summary>
    /// Suggestions derived only from skills the candidate already has that the job
    /// also mentions — never proposes adding skills the candidate doesn't possess.
    /// </summary>
    private static CvTailoringSuggestion HeuristicFallback(CandidateProfile profile, JobPosting job)
    {
        var candidateSkills = profile.CoreSkills.Concat(profile.SecondarySkills)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        var overlap = candidateSkills
            .Where(s => job.ExtractedSkills.Any(js => js.Equals(s, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var highlight = overlap.Count > 0 ? overlap : candidateSkills.Take(5).ToList();

        return new CvTailoringSuggestion(
            SummaryAdjustment: $"Alinhar o resumo ao foco da vaga \"{job.Title}\", enfatizando {string.Join(", ", highlight.Take(3))}.",
            SkillsToHighlight: highlight,
            KeywordsToInclude: job.ExtractedSkills.Concat(job.ExtractedDomains).Distinct().ToList(),
            BulletSuggestions: highlight.Select(s => $"Destacar resultado concreto envolvendo {s}.").ToList(),
            SectionsToReorder: new() { "Mover experiências mais relevantes para o topo." },
            Notes: "Sugestões heurísticas (sem LLM). Não invente experiências; ajuste apenas o que é verdadeiro.");
    }
}
