using Microsoft.EntityFrameworkCore;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Data;

/// <summary>
/// Seeds the candidate profile from the spec plus a couple of sample companies
/// and job postings so the manual match endpoint is testable out of the box.
/// Idempotent: only seeds when the respective tables are empty.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(OpportunityOsDbContext db, CancellationToken ct = default)
    {
        if (!await db.CandidateProfiles.AnyAsync(ct))
        {
            db.CandidateProfiles.Add(new CandidateProfile(
                fullName: "Nícolas Serrano",
                headline: "Desenvolvedor .NET Backend",
                summary: "Backend .NET com experiência em sistemas financeiros, pagamentos, PIX, " +
                         "Open Finance, mensageria, cloud e integrações críticas.",
                location: "Brasil",
                seniority: "Pleno/Sênior",
                preferredLanguage: "pt-BR",
                coreSkills: new[] { ".NET", "C#", "ASP.NET Core", "PostgreSQL", "SQL Server", "AWS", "Kafka", "RabbitMQ", "Docker", "Kubernetes" },
                secondarySkills: new[] { "Azure", "Redis", "Dapper", "EF Core", "Hangfire", "OpenSearch", "Dynatrace", "Sentry", "SonarQube", "Veracode" },
                domains: new[] { "Pagamentos", "PIX", "Open Finance", "Fintech", "Mensageria", "Sistemas distribuídos", "Conciliação", "Webhooks" },
                preferredRoles: new[] { "Backend Engineer .NET", "Software Engineer C#", "Payments Engineer", "Fintech Backend Developer", "Platform Engineer Backend" },
                preferredContractTypes: new[] { "CLT", "PJ", "Contractor" },
                preferredLocations: new[] { "Remote", "Brazil", "LATAM", "Global" }));
        }

        if (!await db.Companies.AnyAsync(ct))
        {
            var fintech = new Company(
                "Sample Fintech", "https://samplefintech.example", "https://boards.greenhouse.io/samplefintech",
                null, "Fintech", "Brazil", CompanyPriority.Strategic, CompanySource.Manual,
                new[] { "fintech", "payments", "pix", "dotnet", "remote-friendly" });
            var generic = new Company(
                "Sample WebShop", "https://webshop.example", "https://jobs.lever.co/webshop",
                null, "E-commerce", "Brazil", CompanyPriority.Low, CompanySource.Manual,
                new[] { "ecommerce" });
            db.Companies.AddRange(fintech, generic);
            await db.SaveChangesAsync(ct);

            db.JobPostings.AddRange(
                new JobPosting(
                    fintech.Id, "seed-001", "Seed", "Senior Backend Engineer (.NET / Payments)",
                    "https://samplefintech.example/jobs/seed-001",
                    "We are hiring a Senior Backend Engineer to build payment and PIX systems using .NET, C#, " +
                    "ASP.NET Core, Kafka and AWS. Remote, Brazil. Experience with Open Finance and fintech is a plus.",
                    location: "Remote - Brazil", language: "en"),
                new JobPosting(
                    generic.Id, "seed-002", "Seed", "Frontend React Developer",
                    "https://webshop.example/jobs/seed-002",
                    "Frontend developer with React, Angular and CSS to build our e-commerce storefront. Onsite.",
                    location: "São Paulo - Onsite", language: "en"));
        }

        await db.SaveChangesAsync(ct);
    }
}
