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
                headline: "Senior Backend Engineer | .NET • C# | Microsserviços • Azure • AWS | Fintech & Pagamentos",
                summary: "Backend Engineer com 6 anos de experiência em sistemas distribuídos de alta " +
                         "criticidade, com forte atuação em .NET (C#) e arquitetura de microsserviços. " +
                         "Vivência sólida em Azure e AWS, mensageria (Kafka, RabbitMQ, Azure Service Bus, SQS), " +
                         "Docker/Kubernetes e CI/CD no Azure DevOps. Experiência consolidada em sistemas " +
                         "financeiros — pagamentos online, PIX, Open Finance e gateways — aplicando DDD, " +
                         "Clean Architecture, SOLID e TDD. Inglês B2 (leitura e escrita técnica fluentes).",
                location: "Rio de Janeiro, Brasil (Remoto)",
                seniority: "Sênior",
                preferredLanguage: "pt-BR",
                coreSkills: new[] { ".NET", "C#", "ASP.NET Core", "Microsserviços", "Event-Driven Architecture", "Kafka", "RabbitMQ", "Azure", "AWS", "Docker", "Kubernetes", "PostgreSQL", "SQL Server" },
                secondarySkills: new[] { "Entity Framework", "Dapper", "Azure Service Bus", "Amazon SQS", "SNS", "DynamoDB", "Redis", "Oracle/PL-SQL", "Node.js", "TypeScript", "Python", "DDD", "CQRS", "Clean Architecture", "Hexagonal Architecture", "SOLID", "TDD", "xUnit", "NUnit", "SonarQube", "Azure DevOps", "CI/CD" },
                domains: new[] { "Pagamentos", "PIX", "Open Finance", "Gateways de pagamento", "Fintech", "Banking", "Mensageria", "Sistemas distribuídos", "Alta criticidade" },
                preferredRoles: new[] { "Senior Backend Engineer", "Backend Engineer .NET", "Software Engineer C#", "Payments Engineer", "Fintech Backend Developer" },
                preferredContractTypes: new[] { "CLT", "PJ", "Contractor" },
                preferredLocations: new[] { "Remote", "Brazil", "LATAM", "Global" },
                experiences: BuildExperiences()));
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

    /// <summary>Real career history (from the candidate's CV) — feeds the AI fit analysis.</summary>
    private static List<CandidateExperience> BuildExperiences() => new()
    {
        new CandidateExperience
        {
            Company = "Impulso",
            Role = "Desenvolvedor .NET Sênior",
            Period = "Set/2024 — Atual",
            Technologies = new() { ".NET", "C#", "AWS Lambda", "App Runner", "SQS", "DynamoDB", "S3", "RDS", "CI/CD" },
            Domains = new() { "Pagamentos", "Gateways de pagamento", "Alto volume transacional" },
            Achievements = new()
            {
                "Microsserviços críticos de pagamento em .NET/C# com DDD, Clean Architecture e SOLID.",
                "APIs escaláveis integradas a gateways, com idempotência e observabilidade.",
                "Automação de deploys via CI/CD, reduzindo tempo de entrega e aumentando confiabilidade."
            }
        },
        new CandidateExperience
        {
            Company = "Stefanini LATAM (cliente: Ailos — Open Finance)",
            Role = "Desenvolvedor .NET",
            Period = "Dez/2023 — Set/2024",
            Technologies = new() { ".NET", "C#", "Kafka", "RabbitMQ", "Docker", "Kubernetes", "AWS", "xUnit", "SonarQube", "Azure DevOps", "Oracle/PL-SQL", "Redis" },
            Domains = new() { "Open Finance", "Banking", "Mensageria" },
            Achievements = new()
            {
                "Funcionalidades críticas de Open Finance (ex.: Chave de Segurança) para cooperativa com +1 milhão de cooperados.",
                "Arquitetura Hexagonal com BFFs, DDD e CQRS para desacoplamento e manutenibilidade.",
                "Framework interno de automação de testes de regressão de APIs (Postman + Newman) no Azure DevOps."
            }
        },
        new CandidateExperience
        {
            Company = "Smart NX",
            Role = "Backend Developer",
            Period = "Jan/2023 — Dez/2023",
            Technologies = new() { "TypeScript", "Node.js", "Amazon Lex", "WebSocket", "Git Flow", "CI/CD" },
            Domains = new() { "IA conversacional", "Tempo real" },
            Achievements = new()
            {
                "Liderança técnica na migração de sistemas legados de JavaScript para TypeScript (Clean Code, TDD, DDD).",
                "Primeira integração de IA da empresa com Amazon Lex.",
                "Definição de padrões de versionamento e pipelines de CI/CD."
            }
        },
        new CandidateExperience
        {
            Company = "Blip",
            Role = "Desenvolvedor .NET",
            Period = "Dez/2021 — Jan/2023",
            Technologies = new() { "C#", "Node.js", "TypeScript", "Azure", "Dialogflow", "IBM Watson", "Amazon Lex" },
            Domains = new() { "Chatbots", "Plataformas conversacionais" },
            Achievements = new()
            {
                "Soluções backend para plataformas de chatbot com integrações complexas de NLP.",
                "Clean Architecture, SOLID e TDD; integrações com Azure focadas em escala e disponibilidade."
            }
        },
        new CandidateExperience
        {
            Company = "Prudentte Gestão de Pagamentos",
            Role = "Full-stack Developer",
            Period = "Mar/2022 — Ago/2022",
            Technologies = new() { "TypeScript", "JavaScript", "React", "Docker", "Kubernetes", "Redis" },
            Domains = new() { "Financeiro" },
            Achievements = new()
            {
                "Features e correções críticas em sistema financeiro.",
                "Design patterns (Composite, Factory, Builder) priorizando escalabilidade."
            }
        },
        new CandidateExperience
        {
            Company = "IK Solution",
            Role = "Desenvolvedor .NET",
            Period = "Mar/2021 — Nov/2021",
            Technologies = new() { ".NET Framework", "ASP.NET", "SQL Server", "React.js" },
            Domains = new() { "Startup" },
            Achievements = new() { "Manutenção e sustentação de sistemas .NET." }
        },
        new CandidateExperience
        {
            Company = "SGC Consultoria & Sistemas",
            Role = "Desenvolvedor .NET",
            Period = "Fev/2020 — Ago/2020",
            Technologies = new() { ".NET Framework", "Entity Framework", "SQL Server" },
            Domains = new() { "Sistemas em produção" },
            Achievements = new() { "Desenvolvimento backend e suporte a sistemas em produção." }
        }
    };
}
