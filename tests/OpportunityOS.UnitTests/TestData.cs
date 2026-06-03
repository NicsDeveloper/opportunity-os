using OpportunityOS.Application.Matching;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.UnitTests;

internal static class TestData
{
    /// <summary>Map a heuristic <see cref="MatchResult"/> to an entity for service tests.</summary>
    public static OpportunityMatch ToEntityForTest(this MatchResult r, Guid jobId, Guid profileId) =>
        new(jobId, profileId, r.OverallScore, r.TechnicalScore, r.DomainScore, r.SeniorityScore,
            r.LocationScore, r.LanguageScore, r.Recommendation, r.Strengths, r.Risks,
            r.MissingRequirements, r.Rationale);

    public static CandidateProfile BackendDotNetProfile() => new(
        fullName: "Nícolas Serrano",
        headline: "Desenvolvedor .NET Backend",
        summary: "Backend .NET, pagamentos, PIX, mensageria, cloud.",
        location: "Brasil",
        seniority: "Pleno/Sênior",
        preferredLanguage: "pt-BR",
        coreSkills: new[] { ".NET", "C#", "ASP.NET Core", "AWS", "Kafka", "RabbitMQ" },
        domains: new[] { "Pagamentos", "PIX", "Open Finance", "Mensageria" },
        preferredWorkModes: new[] { "Remote" });

    public static CandidateProfile JavaBackendProfile() => new(
        fullName: "Java Backend Dev",
        headline: "Backend Java/Spring",
        summary: "Backend Java, Spring Boot, microsserviços, mensageria, cloud.",
        location: "Brasil",
        seniority: "Pleno/Sênior",
        preferredLanguage: "pt-BR",
        coreSkills: new[] { "Java", "Spring Boot", "Spring", "Kafka", "AWS", "PostgreSQL" },
        domains: new[] { "Fintech", "Banking" },
        preferredRoles: new[] { "Java Backend Engineer", "Senior Java Developer" },
        preferredWorkModes: new[] { "Remote" },
        displayName: "Java Backend");

    public static CandidateProfile ReactFrontendProfile() => new(
        fullName: "Frontend React Dev",
        headline: "Frontend React/TypeScript",
        summary: "Frontend React, TypeScript, Next.js, design systems.",
        location: "Brasil",
        seniority: "Pleno/Sênior",
        preferredLanguage: "pt-BR",
        coreSkills: new[] { "React", "TypeScript", "Next.js", "CSS", "Design System" },
        domains: new[] { "SaaS", "Product" },
        preferredRoles: new[] { "Frontend Engineer", "React Developer" },
        preferredWorkModes: new[] { "Remote" },
        displayName: "Frontend React");

    public static CandidateProfile DataEngineerProfile() => new(
        fullName: "Data Engineer Dev",
        headline: "Data Engineer Python/Spark",
        summary: "Data engineer, pipelines, ETL, Python/Spark, cloud.",
        location: "Brasil",
        seniority: "Pleno/Sênior",
        preferredLanguage: "pt-BR",
        coreSkills: new[] { "Python", "SQL", "Airflow", "Spark", "AWS Glue", "Athena" },
        domains: new[] { "Data", "Analytics" },
        preferredRoles: new[] { "Data Engineer", "Senior Data Engineer" },
        preferredWorkModes: new[] { "Remote" },
        displayName: "Data Engineer");

    public static JobPosting Job(string title, string description, string? location = null, string? language = null) =>
        new(
            companyId: Guid.NewGuid(),
            externalId: Guid.NewGuid().ToString(),
            sourceProvider: "Test",
            title: title,
            absoluteUrl: "https://example.com/job",
            descriptionText: description,
            location: location,
            language: language);
}
