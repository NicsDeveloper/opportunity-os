using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

/// <summary>Builds the canonical text used to embed a profile or a job (what the vector represents).</summary>
public static class EmbeddingTexts
{
    public static string ForProfile(CandidateProfile p) => string.Join(" \n ", new[]
    {
        p.Headline, p.Summary,
        "Skills: " + string.Join(", ", p.CoreSkills.Concat(p.SecondarySkills)),
        "Domínios: " + string.Join(", ", p.Domains),
        "Cargos: " + string.Join(", ", p.PreferredRoles),
        p.Seniority,
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static string ForJob(JobPosting j) => string.Join(" \n ", new[]
    {
        j.Title,
        "Skills: " + string.Join(", ", j.ExtractedSkills),
        "Domínios: " + string.Join(", ", j.ExtractedDomains),
        j.DescriptionText,
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>True when the entity has no embedding yet or it was made by a different model.</summary>
    public static bool NeedsEmbedding(string? currentModel, string targetModel) =>
        !string.Equals(currentModel, targetModel, StringComparison.Ordinal);
}
