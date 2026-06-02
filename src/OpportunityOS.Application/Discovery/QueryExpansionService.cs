namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Expands a small set of seeds into many concrete search queries by combining dimensions
/// (role × stack × work-mode × domain) and applying source-scoped templates (site:...).
/// No LLM — pure combinatorics. The Firehose collects broadly, so this aims for volume.
/// </summary>
public sealed class QueryExpansionService
{
    public static readonly string[] Roles =
    {
        "desenvolvedor .net", "desenvolvedor c#", "programador .net", "programador c#",
        "engenheiro de software .net", "software engineer .net", "backend developer .net",
        "backend engineer c#", ".net developer", "c# developer", "dotnet developer",
        "software engineer csharp", "asp.net developer", "desenvolvedor asp.net", "backend .net",
    };

    public static readonly string[] Seniority =
    {
        "pleno", "sênior", "senior", "sr", "middle", "mid-level", "especialista", "tech lead", "lead",
    };

    public static readonly string[] Stack =
    {
        ".NET", "C#", "ASP.NET Core", ".NET Core", ".NET 6", ".NET 7", ".NET 8", ".NET 9", ".NET 10",
        "Azure", "AWS", "Kafka", "RabbitMQ", "SQL Server", "PostgreSQL", "Microservices",
        "Microsserviços", "REST API", "Backend",
    };

    public static readonly string[] WorkModes =
    {
        "remoto", "remote", "home office", "híbrido", "hybrid", "Brasil", "Brazil", "LATAM",
        "contractor", "PJ", "CLT",
    };

    public static readonly string[] Domains =
    {
        "fintech", "pagamentos", "payments", "pix", "open finance", "banco", "banking",
        "financial services", "cartão", "crédito", "gateway", "adquirente", "subadquirente",
    };

    public static readonly string[] SourceFilters =
    {
        "site:gupy.io", "site:jobs.lever.co", "site:boards.greenhouse.io",
        "site:job-boards.greenhouse.io", "site:ashbyhq.com", "site:smartrecruiters.com",
        "site:myworkdayjobs.com", "site:linkedin.com/jobs/view", "site:programathor.com.br",
        "site:geekhunter.com.br", "site:coodesh.com", "site:remotar.com.br", "site:trampos.co",
        // P13 platforms reached via the Firehose (dedicated fetch providers deferred per spec):
        "site:teamtailor.com", "site:recruitee.com", "site:jobs.workable.com",
        "site:breezy.hr", "site:apinfo.com",
        // ATS brasileiros muito usados por consultorias/fintechs (cauda longa real):
        "site:quickin.io", "site:solides.com.br", "site:kenoby.com", "site:jobconvo.com",
        "site:99jobs.com", "site:abler.com.br", "site:pandape.com", "site:inhire.app",
    };

    /// <summary>
    /// Build a broad set of queries for an aggressive campaign. Seeds (campaign keywords)
    /// are treated as extra roles. Produces at least a few hundred distinct queries before
    /// the cap; callers pass maxQueries to bound the run.
    /// </summary>
    public IReadOnlyList<string> ExpandAggressive(IEnumerable<string> seeds, int maxQueries)
    {
        var roles = Roles.Concat(seeds ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList();

        var queries = new List<string>();

        // 1. role × stack × work-mode (core volume)
        foreach (var role in roles)
            foreach (var stack in Stack)
                foreach (var mode in new[] { "remoto", "remote", "Brasil", "home office", "híbrido" })
                    queries.Add($"\"{role}\" \"{stack}\" \"{mode}\"");

        // 2. role × stack × seniority × location
        foreach (var role in roles)
            foreach (var stack in new[] { ".NET", "C#", "ASP.NET Core", "Backend" })
                foreach (var sen in new[] { "pleno", "sênior", "senior", "tech lead" })
                    queries.Add($"\"{role}\" \"{stack}\" \"{sen}\" \"Brasil\"");

        // 3. role × domain (fintech focus)
        foreach (var role in roles)
            foreach (var domain in Domains)
                queries.Add($"\"{role}\" \"{domain}\"");

        // 4. source-scoped (site:) × role × stack
        foreach (var src in SourceFilters)
            foreach (var role in new[] { ".net developer", "c# developer", "desenvolvedor .net" })
                foreach (var stack in new[] { ".NET", "C#", "Backend" })
                    queries.Add($"{src} \"{role}\" \"{stack}\"");

        return Dedupe(queries, maxQueries);
    }

    /// <summary>Targeted queries for a specific company (Bacen / consulting sweeps).</summary>
    public IReadOnlyList<string> ExpandForCompany(string companyName, int maxQueries)
    {
        var c = companyName.Trim();
        var templates = new[]
        {
            $"\"{c}\" \".NET\" vaga",
            $"\"{c}\" \"C#\" backend",
            $"\"{c}\" \"desenvolvedor .NET\"",
            $"\"{c}\" \"engenheiro de software\" \".NET\"",
            $"\"{c}\" \"software engineer\" \"C#\"",
            $"\"{c}\" \"backend developer\" payments",
            $"\"{c}\" Pix developer",
            $"\"{c}\" \"Open Finance\" developer",
            $"\"{c}\" \"trabalhe conosco\" \".NET\"",
            $"\"{c}\" careers \".NET\"",
            $"\"{c}\" \"C#\" \"remoto\"",
            $"\"{c}\" \"backend\" \"remoto\"",
            $"\"{c}\" \"analista desenvolvedor .NET\"",
            $"\"{c}\" \"desenvolvedor c# sênior\"",
        };
        return Dedupe(templates, maxQueries);
    }

    private static IReadOnlyList<string> Dedupe(IEnumerable<string> queries, int maxQueries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var q in queries)
        {
            var trimmed = q.Trim();
            if (trimmed.Length == 0 || !seen.Add(trimmed)) continue;
            result.Add(trimmed);
            if (maxQueries > 0 && result.Count >= maxQueries) break;
        }
        return result;
    }
}
