using System.Globalization;
using System.Text;

namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Turns a legal company name ("BANCO BTG PACTUAL S.A.") into candidate domain
/// slugs ("btgpactual", "bancobtgpactual") for website discovery. Pure/testable.
/// </summary>
public static class CompanyNameNormalizer
{
    private static readonly string[] Suffixes =
    {
        "s.a.", "s/a", " sa", "ltda", "epp", "eireli", " me", "s.a", "scfi",
        "instituição de pagamento", "instituicao de pagamento", " ip", "dtvm",
        "ltda.", "s.a.s", "holding"
    };

    private static readonly string[] LeadingWords =
    {
        "banco", "instituição", "instituicao", "sociedade", "cooperativa", "corretora"
    };

    /// <summary>Brand tokens (e.g. ["btgpactual","bancobtgpactual"]) ordered by likelihood.</summary>
    public static IReadOnlyList<string> BrandSlugs(string name)
    {
        var cleaned = StripSuffixes(Deaccent(name).ToLowerInvariant());
        var words = cleaned.Split(new[] { ' ', '-', '.', '/', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 0).ToList();
        if (words.Count == 0) return Array.Empty<string>();

        var full = string.Concat(words.Select(OnlyAlphaNum)).Trim();
        var withoutLeading = words.Count > 1 && LeadingWords.Contains(words[0])
            ? string.Concat(words.Skip(1).Select(OnlyAlphaNum))
            : null;

        var slugs = new List<string>();
        void Add(string? s) { if (!string.IsNullOrWhiteSpace(s) && !slugs.Contains(s!)) slugs.Add(s!); }
        Add(withoutLeading); // "btgpactual"
        Add(full);           // "bancobtgpactual"
        return slugs;
    }

    /// <summary>Candidate URLs to probe, ordered (BR TLDs first).</summary>
    public static IReadOnlyList<string> CandidateUrls(string name)
    {
        var tlds = new[] { ".com.br", ".com", ".co" };
        var urls = new List<string>();
        foreach (var slug in BrandSlugs(name))
            foreach (var tld in tlds)
                urls.Add($"https://www.{slug}{tld}");
        return urls;
    }

    /// <summary>The main brand token used to validate a candidate page (reduce false positives).</summary>
    public static string? PrimaryToken(string name) => BrandSlugs(name).FirstOrDefault();

    private static string StripSuffixes(string s)
    {
        foreach (var suf in Suffixes)
            s = s.Replace(suf, " ");
        return s;
    }

    private static string OnlyAlphaNum(string s) =>
        new(s.Where(char.IsLetterOrDigit).ToArray());

    private static string Deaccent(string s)
    {
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
