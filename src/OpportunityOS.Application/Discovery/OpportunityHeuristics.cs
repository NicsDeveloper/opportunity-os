using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Shared, pure heuristics used by BOTH the live board (/api/matches) and the daily digest, so the
/// e-mail shows exactly the same quality of opportunities as the screen: real employer (never the
/// ATS/aggregator host), national-vs-international detection, and cross-source de-duplication.
/// </summary>
public static class OpportunityHeuristics
{
    private static readonly string[] BrSignals =
        { "brasil", "brazil", "latam", "remoto", "são paulo", "sao paulo", "rio de janeiro",
          "belo horizonte", "curitiba", "porto alegre", "florianópolis", "florianopolis",
          "recife", "campinas", "brasília", "brasilia", "clt", "pj", "pessoa jurídica" };
    private static readonly string[] IntlSignals =
        { "united states", " usa", "u.s.", "united kingdom", " uk ", "newcastle", "london",
          "europe", "european", "canada", "india", "poland", "germany", "spain", "ireland",
          "amsterdam", "berlin", "lisbon", "portugal", "est time zone", "cet ", "gmt", "anywhere" };

    /// <summary>BR signal -> national; else an explicit foreign signal -> international.</summary>
    public static bool IsInternational(JobPosting j)
    {
        var t = $"{j.Title} {j.Location} {j.DescriptionText}".ToLowerInvariant();
        if (BrSignals.Any(b => t.Contains(b))) return false;
        return IntlSignals.Any(x => t.Contains(x));
    }

    private static readonly string[] NotCompanyToken =
        { "remoto", "remote", "brasil", "brazil", "latam", "home", "office", "híbrido", "hibrido",
          "presencial", "casa", "gupy", "recrutei", "linkedin", "indeed", "glassdoor",
          "desenvolvedor", "desenvolvedora", "analista", "engineer", "engenheir", "developer",
          "senior", "sênior", "pleno", "junior", "júnior", "vaga", "programador", "fullstack", "backend" };

    /// <summary>Company embedded in the title — only high-confidence "at/na/em COMPANY" forms.</summary>
    public static string? CompanyFromTitle(string title)
    {
        foreach (Match m in Regex.Matches(title,
            @"(?:\bat\s+|\bna\s+|\bem\s+|@\s*)([A-Z][\w&.'\-]+(?:\s+[A-Z][\w&.'\-]+){0,2})"))
        {
            var c = m.Groups[1].Value.Trim().TrimEnd('-', '|', '–', ',', '.', ' ');
            var cl = c.ToLowerInvariant();
            if (c.Length >= 2 && !NotCompanyToken.Any(n => cl.Contains(n))) return c;
        }
        return null;
    }

    private static readonly string[] HostNames =
        { "lever", "greenhouse", "gupy", "ashby", "smartrecruiters", "workable", "recruitee", "teamtailor",
          "jobrapido", "jobijoba", "jobleads", "instagram", "facebook", "reddit", "remoteleaf", "dailyremote",
          "simplyhired", "jobgether", "remotejobs", "himalayas", "adzuna", "buscojobs", "talent", "indeed",
          "glassdoor", "linkedin", "careers", "vaga de emprego", "trabajo", "empregos", "jooble" };
    public static bool IsHostName(string? n) =>
        !string.IsNullOrWhiteSpace(n) && HostNames.Any(h => n!.Trim().ToLowerInvariant() == h || n.Trim().ToLowerInvariant().StartsWith(h));

    /// <summary>Real employer to show: real name -> from title -> board name (if official, non-host) -> "a confirmar".</summary>
    public static string BestCompany(JobPosting j, string? storedCompanyName)
    {
        if (!string.IsNullOrWhiteSpace(j.RealCompanyName) && !IsHostName(j.RealCompanyName)) return j.RealCompanyName!;
        var fromTitle = CompanyFromTitle(j.Title);
        if (fromTitle is not null) return fromTitle;
        if (j.SourceType is SourceType.OfficialAts or SourceType.OfficialCareerPage
            && !string.IsNullOrWhiteSpace(storedCompanyName) && !IsHostName(storedCompanyName)) return storedCompanyName!;
        return "Empresa a confirmar";
    }

    public static string NormForDedup(string s)
    {
        var lowered = (s ?? "").ToLowerInvariant();
        var noAccent = new string(lowered.Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(noAccent, @"[^a-z0-9]+", " ").Trim();
    }

    /// <summary>Key that collapses the same posting across sources: company + normalized title.</summary>
    public static string DedupKey(JobPosting j)
    {
        var title = NormForDedup(j.Title);
        var co = !string.IsNullOrWhiteSpace(j.RealCompanyName) ? j.RealCompanyName : CompanyFromTitle(j.Title);
        if (!string.IsNullOrWhiteSpace(co)) return "c|" + NormForDedup(co) + "|" + title;
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (title.Length >= 32 && words >= 5) return "t|" + title;
        return j.Id.ToString();
    }
}
