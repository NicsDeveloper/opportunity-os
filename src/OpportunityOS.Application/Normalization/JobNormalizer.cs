using OpportunityOS.Application.Matching;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Normalization;

public sealed record NormalizationResult(
    string? Seniority,
    string? WorkMode,
    string? Language,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Domains);

public interface IJobNormalizer
{
    NormalizationResult Normalize(JobPosting job);
    NormalizationResult Normalize(string title, string? location, string descriptionText, string? declaredLanguage);
}

/// <summary>
/// Transparent, dependency-free heuristic normalization. No LLM here (Phase 1).
/// </summary>
public sealed class JobNormalizer : IJobNormalizer
{
    public NormalizationResult Normalize(JobPosting job) =>
        Normalize(job.Title, job.Location, job.DescriptionText, job.Language);

    public NormalizationResult Normalize(string title, string? location, string descriptionText, string? declaredLanguage)
    {
        var haystack = $"{title} {location} {descriptionText}".ToLowerInvariant();

        var seniority = FirstMatch(haystack, KnownTerms.SeniorityMap);
        var workMode = FirstMatch(haystack, KnownTerms.WorkModeMap);
        var language = declaredLanguage ?? DetectLanguage(haystack);
        var skills = CollectLabels(haystack, KnownTerms.SkillMap);
        var domains = CollectLabels(haystack, KnownTerms.DomainMap);

        return new NormalizationResult(seniority, workMode, language, skills, domains);
    }

    private static string? FirstMatch(string haystack, (string Token, string Label)[] map)
    {
        foreach (var (token, label) in map)
            if (haystack.Contains(token, StringComparison.Ordinal))
                return label;
        return null;
    }

    private static IReadOnlyList<string> CollectLabels(string haystack, (string Token, string Label)[] map)
    {
        var result = new List<string>();
        foreach (var (token, label) in map)
            if (haystack.Contains(token, StringComparison.Ordinal) && !result.Contains(label))
                result.Add(label);
        return result;
    }

    private static string? DetectLanguage(string haystack)
    {
        // Lightweight signal: a few common Portuguese stop-words vs. English.
        string[] ptSignals = { " e ", " de ", " para ", "experiência", "vaga", "requisitos", "desenvolvedor" };
        string[] enSignals = { " and ", " the ", " for ", "experience", "requirements", "developer", "we are" };

        var pt = ptSignals.Count(s => haystack.Contains(s, StringComparison.Ordinal));
        var en = enSignals.Count(s => haystack.Contains(s, StringComparison.Ordinal));
        if (pt == 0 && en == 0) return null;
        return pt >= en ? "pt-BR" : "en";
    }
}
