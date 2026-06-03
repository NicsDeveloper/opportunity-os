using System.Text.RegularExpressions;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Heuristic company resolver (Priority 5). Extracts the real company from the title/host;
/// never trusts an aggregator host as the company. Returns low confidence + a clear reason
/// ("empresa não confirmada") when it can't tell, instead of guessing.
/// </summary>
public sealed partial class CompanyNameResolver : ICompanyNameResolver
{
    // Source/aggregator words that may appear as a trailing " - X" / " | X" segment in titles.
    private static readonly string[] SourceWords =
    {
        "jobgether", "linkedin", "indeed", "glassdoor", "gupy", "greenhouse", "lever", "ashby",
        "smartrecruiters", "workday", "ziprecruiter", "simplyhired", "bebee", "catho", "reddit",
        "remotar", "programathor", "geekhunter", "coodesh", "trampos", "remoteok", "remote rocketship",
        "vagas.com", "remotejobs", "jooble", "neuvoo", "hacker news", "hnhiring",
    };

    public Task<CompanyResolutionResult> ResolveAsync(RawJobCandidate candidate, CancellationToken ct) =>
        Task.FromResult(Resolve(candidate.Title, candidate.DiscoveredUrl, candidate.SourceName, candidate.SourceType));

    public CompanyResolutionResult Resolve(string? title, string url, string sourceName, SourceType sourceType)
    {
        // 1. Official ATS / career page: the host itself encodes the company — trust it.
        if (sourceType is SourceType.OfficialAts or SourceType.OfficialCareerPage)
        {
            var fromHost = CompanyFromHost(url);
            if (!string.IsNullOrWhiteSpace(fromHost))
                return new(fromHost, sourceName, sourceType, 85, "Empresa derivada do host do ATS/carreira");
        }

        // 2. Title patterns (work for any source — including aggregators, which is the point).
        var fromTitle = CompanyFromTitle(title);
        if (!string.IsNullOrWhiteSpace(fromTitle))
        {
            var conf = sourceType is SourceType.Aggregator or SourceType.SocialIndexed ? 60 : 70;
            return new(fromTitle, sourceName, sourceType, conf, "Empresa extraída do título");
        }

        // 3. Non-aggregator generic web result: host may be the company's own domain.
        if (sourceType is SourceType.SearchResult or SourceType.JobBoard)
        {
            var fromHost = CompanyFromHost(url);
            if (!string.IsNullOrWhiteSpace(fromHost))
                return new(fromHost, sourceName, sourceType, 45, "Empresa possivelmente derivada do domínio");
        }

        // 4. Aggregator/social without a clear company in the title -> do NOT guess the host.
        return new(null, sourceName, sourceType, 0, "Empresa não confirmada");
    }

    private static string? CompanyFromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var t = title.Trim();

        // Strip a trailing source segment: "... - Jobgether", "... | LinkedIn".
        foreach (var sep in new[] { " - ", " | ", " — ", " · " })
        {
            var idx = t.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                var tail = t[(idx + sep.Length)..].Trim();
                if (IsSourceWord(tail)) t = t[..idx].Trim();
            }
        }

        // "[FORTIS SRT] ..." -> bracketed tag is the company.
        var bracket = BracketRegex().Match(t);
        if (bracket.Success)
        {
            var inside = bracket.Groups[1].Value.Trim();
            if (LooksLikeCompany(inside)) return Clean(inside);
        }

        // "... at MARGO" / "... na Stone" -> token after the connector.
        var at = AtRegex().Match(t);
        if (at.Success)
        {
            var c = at.Groups[1].Value.Trim();
            if (LooksLikeCompany(c)) return Clean(c);
        }

        // "<Role> - <Company>" / "<Role> @ <Company>" -> trailing segment if it looks like a company.
        foreach (var sep in new[] { " @ ", " - ", " | " })
        {
            var idx = t.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                var tail = t[(idx + sep.Length)..].Trim();
                if (!IsSourceWord(tail) && LooksLikeCompany(tail) && !LooksLikeRole(tail)) return Clean(tail);
            }
        }

        return null;
    }

    private static string? CompanyFromHost(string url)
    {
        string host, path;
        try { var u = new Uri(url); host = u.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase); path = u.AbsolutePath; }
        catch { return null; }

        // Platforms that encode the company as the first path segment:
        //   boards.greenhouse.io/<company>, jobs.lever.co/<handle>, jobs.quickin.io/<company>/jobs/<id>, ...
        var pathToken = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        string[] pathSlugHosts = { "greenhouse.io", "lever.co", "ashbyhq.com", "smartrecruiters.com",
            "quickin.io", "kenoby.com", "jobconvo.com", "99jobs.com" };
        if (pathSlugHosts.Any(h => host.Contains(h, StringComparison.OrdinalIgnoreCase)))
            return string.IsNullOrWhiteSpace(pathToken) || pathToken is "jobs" or "vagas" or "job" ? null : Clean(pathToken);

        // <company>.gupy.io / <tenant>.myworkdayjobs.com / <company>.{solides|abler|pandape|recrutei}... -> subdomain.
        if (host.Contains("gupy.io") || host.Contains("myworkdayjobs.com") || host.Contains("workdayjobs.com")
            || host.Contains("solides.com.br") || host.Contains("abler.com.br") || host.Contains("pandape.com")
            || host.Contains("recrutei.com.br"))
        {
            var sub = host.Split('.').FirstOrDefault();
            return string.IsNullOrWhiteSpace(sub) || sub is "jobs" or "boards" or "app" or "vagas" or "carreiras" ? null : Clean(sub);
        }

        // Generic: second-level domain (acme.com -> Acme). Skip obvious non-companies and
        // generic words (B8): never label "Services"/"Jobs"/"Careers" as the company.
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var label = parts[^2];
            if (label.Length >= 3 && !IsSourceWord(label) && !IsGenericLabel(label)) return Clean(label);
        }
        return null;
    }

    private static readonly string[] GenericLabels =
    {
        "services", "service", "jobs", "job", "careers", "career", "vagas", "vaga", "api", "remote",
        "remotejobs", "talent", "talents", "work", "works", "hire", "hiring", "app", "apps", "portal",
        "recruit", "recruiting", "emprego", "empregos", "trampos", "site", "home", "cloud", "web", "tech",
    };

    private static bool IsGenericLabel(string s) =>
        GenericLabels.Any(g => string.Equals(g, s, StringComparison.OrdinalIgnoreCase));

    private static bool IsSourceWord(string s) =>
        SourceWords.Any(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeRole(string s)
    {
        var l = s.ToLowerInvariant();
        return l.Contains("developer") || l.Contains("engineer") || l.Contains("desenvolvedor")
            || l.Contains("programador") || l.Contains("backend") || l.Contains(".net") || l.Contains("c#");
    }

    private static bool LooksLikeCompany(string s) =>
        s.Length is >= 2 and <= 60 && !string.IsNullOrWhiteSpace(s) && s.Any(char.IsLetter);

    private static string Clean(string s) =>
        Regex.Replace(s.Replace('-', ' ').Replace('_', ' ').Trim(), @"\s+", " ");

    [GeneratedRegex(@"\[([^\]]{2,60})\]")]
    private static partial Regex BracketRegex();

    [GeneratedRegex(@"\b(?:at|na|no|@)\s+([A-Z0-9][\w&.\- ]{1,40}?)(?:\s*[-|—·]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex AtRegex();
}
