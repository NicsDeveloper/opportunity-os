using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

public sealed class CareerInsightService : AiServiceBase, ICareerInsightService
{
    public CareerInsightService(
        ILlmProvider llm, IPromptExecutionLogStore audit, ILogger<CareerInsightService> logger)
        : base(llm, audit, logger) { }

    protected override string ServiceName => "CareerInsight";

    public Task<CareerInsightReport> GenerateInsightsAsync(
        CandidateProfile profile, IReadOnlyCollection<JobPosting> analyzedJobs, CancellationToken ct) =>
        ExecuteAsync(
            Prompts.CareerInsights(profile, analyzedJobs),
            () => HeuristicFallback(profile, analyzedJobs),
            jobId: null, profileId: profile.Id, ct);

    /// <summary>Aggregate extracted skills/domains across the jobs to surface patterns.</summary>
    private static CareerInsightReport HeuristicFallback(
        CandidateProfile profile, IReadOnlyCollection<JobPosting> jobs)
    {
        var skillFreq = Frequency(jobs.SelectMany(j => j.ExtractedSkills));
        var domainFreq = Frequency(jobs.SelectMany(j => j.ExtractedDomains));

        var candidateSkills = profile.CoreSkills.Concat(profile.SecondarySkills)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var gaps = skillFreq.Keys.Where(s => !candidateSkills.Contains(s)).Take(8).ToList();
        var topTech = skillFreq.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(8).ToList();
        var topDomains = domainFreq.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(6).ToList();

        return new CareerInsightReport(
            MostRequestedTechnologies: topTech,
            RecurringGaps: gaps,
            StrongestDomains: topDomains,
            StudySuggestions: gaps.Select(g => $"Aprofundar em {g}.").ToList(),
            PostIdeas: topDomains.Select(d => $"Post técnico sobre {d} na prática.").ToList(),
            MostPromisingCompanies: new(),
            Summary: $"Análise heurística de {jobs.Count} vaga(s): tecnologias mais pedidas " +
                     $"{string.Join(", ", topTech.Take(3))}.");
    }

    private static Dictionary<string, int> Frequency(IEnumerable<string> items)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            dict[item] = dict.TryGetValue(item, out var c) ? c + 1 : 1;
        }
        return dict;
    }
}
