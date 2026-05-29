using OpportunityOS.Domain.Entities;

namespace OpportunityOS.UnitTests;

internal static class TestData
{
    public static CandidateProfile BackendDotNetProfile() => new(
        fullName: "Nícolas Serrano",
        headline: "Desenvolvedor .NET Backend",
        summary: "Backend .NET, pagamentos, PIX, mensageria, cloud.",
        location: "Brasil",
        seniority: "Pleno/Sênior",
        preferredLanguage: "pt-BR",
        coreSkills: new[] { ".NET", "C#", "ASP.NET Core", "AWS", "Kafka", "RabbitMQ" },
        domains: new[] { "Pagamentos", "PIX", "Open Finance", "Mensageria" });

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
