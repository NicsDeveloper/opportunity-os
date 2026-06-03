using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Matching;

/// <summary>The broad technology family a role/profile primarily belongs to.</summary>
public enum StackFamily
{
    DotNet, Java, Frontend, Node, Python, DataEngineering, Qa, Mobile, Go, Php, Ruby
}

/// <summary>
/// A profile's stack signature: which families it is strong in (core), competent in
/// (secondary), and which it wants demoted (excluded). Computed from its skill lists.
/// </summary>
public sealed record ProfileStackSignature(
    IReadOnlySet<StackFamily> Core,
    IReadOnlySet<StackFamily> Secondary,
    IReadOnlySet<StackFamily> Excluded);

/// <summary>
/// Transparent, LLM-free mapping of skills/postings to a small set of stack families.
/// v1 on purpose: a handful of families with explicit tokens — enough to prove
/// ".NET ≠ Java ≠ React ≠ Data", not to model the whole market.
/// </summary>
public static class StackTaxonomy
{
    // Family -> identifying tokens (lowercase, substring match). Supporting/cross-cutting
    // skills (AWS, Kafka, PostgreSQL, Docker…) are intentionally NOT here: they don't
    // decide a family, they only reinforce a match (handled by the engine).
    private static readonly (StackFamily Family, string[] Tokens)[] Families =
    {
        (StackFamily.DotNet, new[] { ".net", "dotnet", "c#", "csharp", "asp.net", "aspnet", "blazor", "entity framework", "ef core" }),
        (StackFamily.Java, new[] { "java", "spring boot", "spring", "hibernate", "quarkus", "micronaut", "jakarta", "j2ee", "jvm" }),
        (StackFamily.Frontend, new[] { "react", "angular", "vue", "svelte", "next.js", "nextjs", "typescript", "javascript", "frontend", "front-end", "front end", "tailwind", "redux", "design system" }),
        (StackFamily.Node, new[] { "node.js", "nodejs", "express", "nestjs", "node " }),
        (StackFamily.Python, new[] { "python", "django", "flask", "fastapi", "pandas" }),
        (StackFamily.DataEngineering, new[] { "airflow", "spark", "databricks", "aws glue", "glue", "athena", "etl", "data engineer", "data engineering", "data pipeline", "dbt", "hadoop", "snowflake", "redshift", "bigquery", "data lake", "data warehouse" }),
        (StackFamily.Qa, new[] { "qa", "quality assurance", "test automation", "selenium", "cypress", "playwright", "sdet", "testes automatizados", "quality engineer", "automação de teste", "automation tester" }),
        (StackFamily.Mobile, new[] { "android", "ios", "swift", "flutter", "react native", "jetpack compose", "mobile developer", "desenvolvedor mobile" }),
        (StackFamily.Go, new[] { "golang", " go ", "go developer" }),
        (StackFamily.Php, new[] { "php", "laravel", "symfony", "wordpress" }),
        (StackFamily.Ruby, new[] { "ruby", "rails", "ruby on rails" }),
    };

    /// <summary>Families whose tokens appear anywhere in the given text.</summary>
    public static HashSet<StackFamily> FamiliesIn(string text)
    {
        var lower = text.ToLowerInvariant();
        var found = new HashSet<StackFamily>();
        foreach (var (family, tokens) in Families)
            if (tokens.Any(t => lower.Contains(t, StringComparison.Ordinal)))
                found.Add(family);
        return found;
    }

    /// <summary>Build a profile's stack signature from its core/secondary/excluded skill lists.</summary>
    public static ProfileStackSignature Detect(CandidateProfile profile)
    {
        var core = FamiliesIn(string.Join(" ", profile.CoreSkills));
        var secondary = FamiliesIn(string.Join(" ", profile.SecondarySkills));
        secondary.ExceptWith(core); // a family that is core is not also "merely" secondary
        var excluded = FamiliesIn(string.Join(" ", profile.ExcludedStacks));
        return new ProfileStackSignature(core, secondary, excluded);
    }
}
