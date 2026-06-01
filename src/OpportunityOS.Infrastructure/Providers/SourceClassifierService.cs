using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Host-based source classification (Priority 4). Confidence tiers follow the spec:
/// 90 ATS/official career page · 80 Gupy · 70 trusted job board · 50 web (possibly official)
/// · 35 aggregator · 25 social-indexed (LinkedIn) · 15 snippet with no clear company.
/// </summary>
public sealed class SourceClassifierService : ISourceClassifierService
{
    private static readonly string[] AtsHosts =
        { "greenhouse.io", "lever.co", "ashbyhq.com", "smartrecruiters.com", "workdayjobs.com", "myworkdayjobs.com",
          "teamtailor.com", "recruitee.com", "workable.com", "breezy.hr" };
    private static readonly string[] JobBoards =
        { "programathor", "geekhunter", "coodesh", "remotar", "trampos", "apinfo", "vagas.com" };
    private static readonly string[] Aggregators =
        { "indeed.", "glassdoor.", "jobgether", "simplyhired", "ziprecruiter", "bebee", "catho.", "reddit.", "remotejobs", "remoteok", "jooble", "neuvoo" };

    public SourceClassificationResult Classify(string url, string? title, string? snippet)
    {
        var host = TryHost(url);
        bool Has(string[] needles) => needles.Any(n => host.Contains(n, StringComparison.OrdinalIgnoreCase));

        if (Has(AtsHosts))
            return new(SourceType.OfficialAts, host, 90, false, "Host é um ATS oficial conhecido");

        if (host.Contains("gupy.io", StringComparison.OrdinalIgnoreCase))
            return new(SourceType.OfficialAts, host, 80, false, "Gupy (ATS) da empresa");

        if (host.Contains("linkedin.", StringComparison.OrdinalIgnoreCase))
            return new(SourceType.SocialIndexed, "linkedin.com", 25, true, "LinkedIn indexado — revisão manual obrigatória (sem scrape/login)");

        if (Has(Aggregators))
            return new(SourceType.Aggregator, host, 35, true, "Agregador — empresa real precisa ser confirmada");

        if (Has(JobBoards))
            return new(SourceType.JobBoard, host, 70, false, "Job board confiável");

        if (LooksLikeCareerPage(url))
            return new(SourceType.OfficialCareerPage, host, 90, false, "Página oficial de carreira da empresa");

        // Generic web result: possibly the company's own domain. Lower if no company hint.
        var hasCompanyHint = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(snippet);
        return hasCompanyHint
            ? new(SourceType.SearchResult, host, 50, false, "Resultado de busca em domínio possivelmente oficial")
            : new(SourceType.SearchResult, host, 15, true, "Snippet sem empresa clara — revisão manual");
    }

    private static bool LooksLikeCareerPage(string url)
    {
        var u = url.ToLowerInvariant();
        return u.Contains("/careers") || u.Contains("/carreiras") || u.Contains("/jobs")
            || u.Contains("trabalhe-conosco") || u.Contains("trabalheconosco") || u.Contains("/vagas");
    }

    private static string TryHost(string url)
    {
        try { return new Uri(url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase); }
        catch { return url; }
    }
}
