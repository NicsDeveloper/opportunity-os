using System.Text.RegularExpressions;
using OpportunityOS.Application.Matching;
using OpportunityOS.Contracts;

namespace OpportunityOS.Application.Import;

/// <summary>
/// Deterministic LinkedIn → CandidateProfile draft mapping. Normalizes skills via an alias map and
/// classifies core vs secondary using <see cref="StackTaxonomy"/> (boundary-safe, so "java" ≠
/// "javascript"). Infers seniority from experience duration + headline, and domains by keyword.
/// </summary>
public sealed class LinkedInProfileToCandidateProfileDraftMapper : ILinkedInProfileToCandidateProfileDraftMapper
{
    // alias (lowercase, boundary-matched) -> canonical display name.
    private static readonly (string Alias, string Canonical)[] SkillAliases =
    {
        (".net core", ".NET"), (".net", ".NET"), ("dotnet", ".NET"),
        ("asp.net core", "ASP.NET Core"), ("asp.net", "ASP.NET Core"),
        ("c#", "C#"), ("csharp", "C#"),
        ("node.js", "Node.js"), ("nodejs", "Node.js"), ("node", "Node.js"),
        ("typescript", "TypeScript"), ("javascript", "JavaScript"), ("react", "React"),
        ("kafka", "Kafka"), ("rabbitmq", "RabbitMQ"),
        ("docker", "Docker"), ("kubernetes", "Kubernetes"), ("k8s", "Kubernetes"),
        ("sql server", "SQL Server"), ("postgresql", "PostgreSQL"), ("postgres", "PostgreSQL"),
        ("mongodb", "MongoDB"), ("mongo", "MongoDB"), ("redis", "Redis"),
        ("microservices", "Microservices"), ("microsserviços", "Microservices"), ("microsservicos", "Microservices"),
        ("clean architecture", "Clean Architecture"), ("ddd", "DDD"), ("solid", "SOLID"),
        ("xunit", "xUnit"), ("ci/cd", "CI/CD"), ("cicd", "CI/CD"), ("azure devops", "Azure DevOps"),
        // AWS / Cloud services
        ("aws", "AWS"), ("lambda", "AWS Lambda"), ("sqs", "SQS"), ("s3", "S3"),
        ("dynamodb", "DynamoDB"), ("rds", "RDS"),
    };

    // Canonical skills considered CORE when their family is the profile's core (or always-core stacks).
    private static readonly HashSet<string> CloudCanonicals = new(StringComparer.OrdinalIgnoreCase)
        { "AWS", "AWS Lambda", "SQS", "S3", "DynamoDB", "RDS" };

    private static readonly (string Keyword, string Domain)[] DomainKeywords =
    {
        ("pagament", "Pagamentos"), ("payment", "Pagamentos"), ("pix", "Pagamentos"),
        ("open finance", "Open Finance"), ("openfinance", "Open Finance"),
        ("bank", "Banking"), ("banc", "Banking"), ("fintech", "Fintech"),
        ("cloud", "Cloud"), ("aws", "Cloud"), ("chatbot", "Chatbots"),
        ("mensageria", "Mensageria"), ("messaging", "Mensageria"),
        ("high availability", "High Availability"), ("alta disponibilidade", "High Availability"),
        ("crédito", "Crédito"), ("credito", "Crédito"), ("credit", "Crédito"),
    };

    public CandidateProfileDraftDto Map(LinkedInProfileImportDto p)
    {
        var headline = p.Headline ?? "";
        var expText = string.Join(" ", p.Experiences.Select(e => $"{e.Title} {e.Company} {e.Description}"));
        var fullText = $"{headline} {p.Summary} {string.Join(" ", p.Skills)} {expText}".ToLowerInvariant();

        // 1) Detected canonical skills (boundary-matched aliases) + the explicit LinkedIn skills.
        var detected = new List<string>();
        void AddCanonical(string c) { if (!detected.Contains(c, StringComparer.OrdinalIgnoreCase)) detected.Add(c); }
        foreach (var (alias, canonical) in SkillAliases)
            if (WordPresent(fullText, alias)) AddCanonical(canonical);
        foreach (var s in p.Skills) // keep explicit skills we didn't alias (e.g. niche tools)
            if (!SkillAliases.Any(a => a.Canonical.Equals(s, StringComparison.OrdinalIgnoreCase)))
                AddCanonical(s.Trim());

        // 2) Core vs secondary via StackTaxonomy families of the headline + explicit skills.
        var coreFamilies = StackTaxonomy.FamiliesIn($"{headline} {string.Join(" ", p.Skills)}");
        var core = new List<string>();
        var secondary = new List<string>();
        foreach (var skill in detected)
        {
            var fam = StackTaxonomy.FamiliesIn(skill);
            var isCore = (fam.Count > 0 && fam.Overlaps(coreFamilies))  // language family matches the profile core
                         || skill is "Microservices";                   // (specific cloud services stay secondary)
            (isCore ? core : secondary).Add(skill);
        }
        // AWS umbrella as a core skill when any AWS service is present.
        if (detected.Any(CloudCanonicals.Contains) && !core.Contains("AWS")) { core.Add("AWS"); secondary.Remove("AWS"); }

        core = core.Distinct(StringComparer.OrdinalIgnoreCase).Take(14).ToList();
        secondary = secondary.Where(s => !core.Contains(s, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(18).ToList();

        // 3) Domains by keyword.
        var domains = new List<string>();
        foreach (var (kw, dom) in DomainKeywords)
            if (fullText.Contains(kw, StringComparison.OrdinalIgnoreCase) && !domains.Contains(dom))
                domains.Add(dom);

        // 4) Seniority from total experience + headline hints.
        var seniority = InferSeniority(p.Experiences, headline);

        // 5) Preferred roles from headline + recent titles.
        var roles = InferRoles(headline, p.Experiences);

        var displayName = !string.IsNullOrWhiteSpace(headline)
            ? Truncate(headline, 40)
            : (p.FullName ?? "Meu perfil");

        var experiences = p.Experiences.Select(e => new CandidateExperienceDraftDto(
            e.Company, e.Title,
            string.Join(" ", new[] { e.StartDateText, e.EndDateText, e.DurationText is null ? null : $"({e.DurationText})" }
                .Where(x => !string.IsNullOrWhiteSpace(x))),
            StackSkillsIn(e.Description), new List<string>())).ToList();

        return new CandidateProfileDraftDto(
            DisplayName: displayName,
            FullName: p.FullName ?? "",
            Headline: headline,
            Summary: p.Summary ?? "",
            Location: p.Location ?? "",
            Seniority: seniority,
            PreferredLanguage: "pt-BR",
            CoreSkills: core,
            SecondarySkills: secondary,
            ExcludedStacks: new(),
            Domains: domains,
            PreferredRoles: roles,
            PreferredContractTypes: new() { "CLT", "PJ" },
            PreferredLocations: string.IsNullOrWhiteSpace(p.Location) ? new() : new() { p.Location! },
            PreferredWorkModes: new() { "Remote", "Hybrid" },
            MinimumScoreToShow: 60,
            Experiences: experiences);
    }

    private static string InferSeniority(IReadOnlyCollection<LinkedInExperienceDto> experiences, string headline)
    {
        var h = headline.ToLowerInvariant();
        if (h.Contains("tech lead") || h.Contains("staff") || h.Contains("principal") || h.Contains("architect") || h.Contains("arquiteto"))
            return "Sênior";
        if (h.Contains("júnior") || h.Contains("junior") || h.Contains("jr") || h.Contains("trainee") || h.Contains("estági"))
            return "Júnior";

        var months = experiences.Sum(e => MonthsOf(e));
        var years = months / 12.0;
        var bySenior = h.Contains("sênior") || h.Contains("senior") || h.Contains("sr");
        if (years >= 5 || bySenior) return "Pleno/Sênior";
        if (years >= 3) return "Pleno";
        if (years > 0) return "Júnior";
        return "Pleno/Sênior"; // unknown duration but a real headline → conservative default
    }

    private static int MonthsOf(LinkedInExperienceDto e)
    {
        if (!string.IsNullOrWhiteSpace(e.DurationText))
        {
            var d = e.DurationText.ToLowerInvariant();
            var yrs = Regex.Match(d, @"(\d+)\s*(?:ano|year|yr)");
            var mos = Regex.Match(d, @"(\d+)\s*(?:mes|m[êe]s|month)");
            var total = 0;
            if (yrs.Success) total += int.Parse(yrs.Groups[1].Value) * 12;
            if (mos.Success) total += int.Parse(mos.Groups[1].Value);
            if (total > 0) return total;
        }
        // Fallback: year range in start/end.
        var years = Regex.Matches($"{e.StartDateText} {e.EndDateText}", @"\b(19|20)\d{2}\b")
            .Select(m => int.Parse(m.Value)).ToList();
        if (years.Count >= 2) return Math.Max(0, (years[^1] - years[0]) * 12);
        return 0;
    }

    private static List<string> InferRoles(string headline, IReadOnlyCollection<LinkedInExperienceDto> experiences)
    {
        var roles = new List<string>();
        void Add(string r) { r = r.Trim(); if (r.Length is >= 3 and <= 50 && !roles.Contains(r, StringComparer.OrdinalIgnoreCase)) roles.Add(r); }
        var h = headline.ToLowerInvariant();
        if (h.Contains(".net") || h.Contains("c#")) Add("Desenvolvedor .NET");
        if (h.Contains("backend") || h.Contains("back-end") || h.Contains("back end")) Add("Backend Developer");
        if (h.Contains("software")) Add("Software Engineer");
        foreach (var e in experiences.Take(3)) Add(e.Title);
        return roles.Take(6).ToList();
    }

    private static List<string> StackSkillsIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();
        var lower = text.ToLowerInvariant();
        return SkillAliases.Where(a => WordPresent(lower, a.Alias)).Select(a => a.Canonical)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
    }

    // Boundary-aware presence: tokens flanked by non-alphanumerics (so "s3"/"aws" don't false-match,
    // and ".net core" matches as a phrase). Symbols inside the token are escaped.
    private static bool WordPresent(string haystack, string token) =>
        Regex.IsMatch(haystack, $@"(?<![a-z0-9]){Regex.Escape(token)}(?![a-z0-9])", RegexOptions.IgnoreCase);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max].Trim();
}
