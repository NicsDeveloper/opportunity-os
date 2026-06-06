using System.Text.RegularExpressions;

namespace OpportunityOS.Application.Import;

/// <summary>
/// Scores how strongly an extracted text matches the LinkedIn profile-PDF layout. Bilingual
/// (PT-BR / EN). ≥70 = accept; 50–69 = accept with warning; &lt;50 = reject as "not a LinkedIn PDF".
/// </summary>
public sealed class LinkedInPdfProfileDetector : ILinkedInPdfProfileDetector
{
    private static readonly Regex LinkedInUrl = new(@"linkedin\.com/in/", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PageMarker = new(@"\bpage\s+\d+\s+of\s+\d+\b|\bpágina\s+\d+\s+de\s+\d+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // (label, weight, matcher)
    private static readonly (string Label, int Weight, Func<string, bool> Hit)[] Checks =
    {
        ("linkedin-url", 25, t => LinkedInUrl.IsMatch(t) || t.Contains("(LinkedIn)", StringComparison.OrdinalIgnoreCase)),
        ("page-marker", 20, t => PageMarker.IsMatch(t)),
        ("contato", 12, t => HasAny(t, "Contato", "Contact")),
        ("resumo", 12, t => HasAny(t, "Resumo", "Summary")),
        ("experiencia", 12, t => HasAny(t, "Experiência", "Experiencia", "Experience")),
        ("formacao", 9, t => HasAny(t, "Formação acadêmica", "Formacao academica", "Education")),
        ("competencias", 5, t => HasAny(t, "Principais competências", "Principais competencias", "Top Skills", "Skills")),
        ("certificacoes", 5, t => HasAny(t, "Certifications", "Certificações", "Certificacoes", "Licenças e certificados")),
    };

    public LinkedInPdfDetectionResult Detect(string extractedText)
    {
        var text = extractedText ?? string.Empty;
        var signals = new List<string>();
        var missing = new List<string>();
        var score = 0;

        foreach (var (label, weight, hit) in Checks)
        {
            if (hit(text)) { score += weight; signals.Add(label); }
            else missing.Add(label);
        }

        score = Math.Clamp(score, 0, 100);
        return new LinkedInPdfDetectionResult(score >= 50, score, signals, missing);
    }

    private static bool HasAny(string text, params string[] tokens) =>
        tokens.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
}
