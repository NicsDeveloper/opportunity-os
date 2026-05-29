namespace OpportunityOS.Application.Matching;

/// <summary>
/// Curated keyword vocabularies used by the heuristic normalizer and match
/// engine. Kept transparent on purpose (Risk 3 in the spec): the score must be
/// explainable, so every signal lives here in plain sight.
/// </summary>
public static class KnownTerms
{
    // Strong .NET / backend signals.
    public static readonly string[] CoreDotNet =
    {
        ".net", "dotnet", "c#", "csharp", "asp.net", "aspnet", "minimal api",
        "entity framework", "ef core"
    };

    public static readonly string[] BackendSignals =
    {
        "backend", "back-end", "back end", "api", "microservic", "distributed",
        "server-side"
    };

    // Valued supporting stack (the spec's "+ pontuação adicional").
    public static readonly string[] BonusStack =
    {
        "kafka", "rabbitmq", "sqs", "service bus", "aws", "azure", "docker",
        "kubernetes", "postgresql", "postgres", "sql server", "redis",
        "opensearch", "dapper", "hangfire"
    };

    // Competing primary stacks that reduce technical fit.
    public static readonly string[] CompetingLanguages =
    {
        "java", "python", "php", "ruby", "golang", " go ", "node.js", "nodejs"
    };

    public static readonly string[] FrontendSignals =
    {
        "react", "angular", "vue", "frontend", "front-end", "front end",
        "css", "tailwind"
    };

    // Strong domain signals (the spec's high-value domains).
    public static readonly string[] StrongDomains =
    {
        "payments", "payment", "pagamento", "pix", "open finance", "open banking",
        "openfinance", "banking", "fintech", "financial services", "financial",
        "acquir", "conciliation", "conciliação", "webhook"
    };

    public static readonly string[] MediumDomains =
    {
        "marketplace", "e-commerce", "ecommerce", "billing", "subscription",
        "fraud", "antifraud", "risk", "credit", "lending"
    };

    // Seniority heuristics (token -> normalized label).
    public static readonly (string Token, string Label)[] SeniorityMap =
    {
        ("principal", "Principal"),
        ("staff", "Staff"),
        ("senior", "Senior"),
        ("sênior", "Senior"),
        ("sr.", "Senior"),
        ("sr ", "Senior"),
        ("lead", "Lead"),
        ("pleno", "MidLevel"),
        ("mid-level", "MidLevel"),
        ("mid level", "MidLevel"),
        ("junior", "Junior"),
        ("júnior", "Junior"),
        ("jr.", "Junior"),
        ("jr ", "Junior"),
        ("intern", "Intern"),
        ("estági", "Intern"),
        ("trainee", "Intern")
    };

    public static readonly (string Token, string Label)[] WorkModeMap =
    {
        ("remote", "Remote"),
        ("remoto", "Remote"),
        ("anywhere", "Remote"),
        ("híbrido", "Hybrid"),
        ("hibrido", "Hybrid"),
        ("hybrid", "Hybrid"),
        ("onsite", "Onsite"),
        ("on-site", "Onsite"),
        ("presencial", "Onsite")
    };

    // Skills surfaced during normalization (display-friendly labels).
    public static readonly (string Token, string Label)[] SkillMap =
    {
        (".net", ".NET"), ("dotnet", ".NET"), ("c#", "C#"), ("csharp", "C#"),
        ("asp.net", "ASP.NET Core"), ("aspnet", "ASP.NET Core"),
        ("entity framework", "EF Core"), ("ef core", "EF Core"),
        ("dapper", "Dapper"), ("kafka", "Kafka"), ("rabbitmq", "RabbitMQ"),
        ("sqs", "SQS"), ("service bus", "Azure Service Bus"), ("aws", "AWS"),
        ("azure", "Azure"), ("docker", "Docker"), ("kubernetes", "Kubernetes"),
        ("postgresql", "PostgreSQL"), ("postgres", "PostgreSQL"),
        ("sql server", "SQL Server"), ("redis", "Redis"),
        ("opensearch", "OpenSearch"), ("hangfire", "Hangfire")
    };

    public static readonly (string Token, string Label)[] DomainMap =
    {
        ("payment", "Payments"), ("pagamento", "Payments"), ("pix", "PIX"),
        ("open finance", "Open Finance"), ("openfinance", "Open Finance"),
        ("open banking", "Open Finance"), ("banking", "Banking"),
        ("fintech", "Fintech"), ("financial", "Financial Services"),
        ("acquir", "Acquiring"), ("conciliation", "Conciliation"),
        ("conciliação", "Conciliation"), ("webhook", "Webhooks"),
        ("fraud", "Antifraud"), ("credit", "Credit"), ("lending", "Credit")
    };
}
