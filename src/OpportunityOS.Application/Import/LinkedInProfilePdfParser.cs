using System.Text.RegularExpressions;
using OpportunityOS.Contracts;

namespace OpportunityOS.Application.Import;

/// <summary>
/// Deterministic LinkedIn PDF parser. Works on the extractor's line-oriented output (main column
/// first, then sidebar). Sections are located by bilingual headers; email/URL by regex. Experience
/// segmentation is anchored on date/period lines; unsegmentable blocks are kept verbatim + a warning.
/// </summary>
public sealed class LinkedInProfilePdfParser : ILinkedInProfilePdfParser
{
    private static readonly Regex EmailRx = new(@"[\w.\-+]+@[\w\-]+\.[\w.\-]+", RegexOptions.Compiled);
    private static readonly Regex LinkedInRx = new(@"(?:https?://)?(?:www\.)?linkedin\.com/in/[\w\-%]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PageMarkerRx = new(@"^\s*(page\s+\d+\s+of\s+\d+|página\s+\d+\s+de\s+\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex YearRx = new(@"\b(19|20)\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex DurationRx = new(@"\(([^)]*)\)", RegexOptions.Compiled);

    // Section header synonyms (a line equal-ish to one of these starts a section).
    private static readonly string[] HContact = { "contato", "contact" };
    private static readonly string[] HSkills = { "principais competências", "principais competencias", "top skills", "skills", "competências", "competencias" };
    private static readonly string[] HCerts = { "certifications", "certificações", "certificacoes", "licenças e certificados", "licencas e certificados" };
    private static readonly string[] HLanguages = { "idiomas", "languages" };
    private static readonly string[] HSummary = { "resumo", "summary" };
    private static readonly string[] HExperience = { "experiência", "experiencia", "experience" };
    private static readonly string[] HEducation = { "formação acadêmica", "formacao academica", "education", "formação", "formacao" };

    private static readonly string[][] AllHeaders =
        { HContact, HSkills, HCerts, HLanguages, HSummary, HExperience, HEducation };

    public LinkedInProfileImportDto Parse(string extractedText, LinkedInPdfDetectionResult detection, out List<string> warnings)
    {
        warnings = new List<string>();
        var raw = (extractedText ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = raw.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !PageMarkerRx.IsMatch(l))
            .ToList();

        var email = EmailRx.Match(raw) is { Success: true } em ? em.Value : null;
        var linkedIn = LinkedInRx.Match(raw) is { Success: true } li ? li.Value : null;

        // Identity: first meaningful lines of the main column, before any section header / contact line.
        var (fullName, headline, location) = ParseIdentity(lines, email, linkedIn);

        var summary = JoinBlock(SectionLines(lines, HSummary));
        var skills = ParseSkills(SectionLines(lines, HSkills));
        var certs = SectionLines(lines, HCerts).Where(IsContentLine).Distinct().ToList();
        var experiences = ParseExperiences(SectionLines(lines, HExperience), warnings);
        var education = ParseEducation(SectionLines(lines, HEducation));

        if (detection.Confidence is >= 50 and < 70)
            warnings.Add("Este PDF não parece 100% no padrão LinkedIn; revise os campos com atenção.");
        if (skills.Count == 0) warnings.Add("Nenhuma competência foi detectada automaticamente; adicione suas skills.");

        var confidence = new ProfileImportConfidenceDto(detection.Confidence, detection.Signals, detection.MissingSignals);
        return new LinkedInProfileImportDto(
            fullName, headline, location, email, linkedIn, summary,
            skills, certs, experiences, education, confidence);
    }

    private static (string? Name, string? Headline, string? Location) ParseIdentity(List<string> lines, string? email, string? linkedIn)
    {
        var candidates = lines
            .Where(l => !IsAnyHeader(l) && !l.Equals(email, StringComparison.OrdinalIgnoreCase)
                        && (linkedIn is null || !l.Contains("linkedin.com/in", StringComparison.OrdinalIgnoreCase))
                        && !EmailRx.IsMatch(l))
            .Take(3).ToList();
        var name = candidates.ElementAtOrDefault(0);
        var headline = candidates.ElementAtOrDefault(1);
        var location = candidates.ElementAtOrDefault(2);
        // A location line is short and comma/region-ish; if the 3rd line looks long it's probably headline overflow.
        if (location is { Length: > 60 }) location = null;
        return (name, headline, location);
    }

    private static List<string> ParseSkills(List<string> sectionLines)
    {
        var skills = new List<string>();
        foreach (var line in sectionLines.Where(IsContentLine))
        {
            // Skills sometimes arrive comma- or bullet-separated on one line.
            foreach (var part in line.Split(new[] { ',', '•', '·', '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = part.Trim();
                if (s.Length is >= 2 and <= 60 && !skills.Contains(s, StringComparer.OrdinalIgnoreCase))
                    skills.Add(s);
            }
        }
        return skills;
    }

    private static List<LinkedInEducationDto> ParseEducation(List<string> sectionLines)
    {
        var content = sectionLines.Where(IsContentLine).ToList();
        var result = new List<LinkedInEducationDto>();
        // Heuristic: institution line, optional degree/field line, optional period line.
        for (var i = 0; i < content.Count; i++)
        {
            var institution = content[i];
            string? degree = null; string? period = null;
            if (i + 1 < content.Count && !LooksLikePeriod(content[i + 1])) { degree = content[++i]; }
            if (i + 1 < content.Count && LooksLikePeriod(content[i + 1])) { period = content[++i]; }
            result.Add(new LinkedInEducationDto(institution, degree, null, period));
        }
        return result;
    }

    private static List<LinkedInExperienceDto> ParseExperiences(List<string> sectionLines, List<string> warnings)
    {
        var content = sectionLines.Where(IsContentLine).ToList();
        if (content.Count == 0) return new();

        // Anchor on period lines. LinkedIn order per role: Company, Title, Period[, Location], Description.
        var anchors = Enumerable.Range(0, content.Count).Where(i => LooksLikePeriod(content[i])).ToList();
        if (anchors.Count == 0)
        {
            warnings.Add("Não foi possível segmentar as experiências automaticamente; o texto foi mantido para revisão.");
            return new() { new LinkedInExperienceDto("(empresa a revisar)", "(cargo a revisar)", null, null, null, null, JoinBlock(content)) };
        }

        var result = new List<LinkedInExperienceDto>();
        for (var a = 0; a < anchors.Count; a++)
        {
            var p = anchors[a];
            var title = p - 1 >= 0 ? content[p - 1] : null;
            var company = p - 2 >= 0 && !LooksLikePeriod(content[p - 2]) ? content[p - 2] : title;
            var (startText, endText, duration) = SplitPeriod(content[p]);

            // Description: lines after the period up to the next role's company (2 before next anchor).
            var descEnd = a + 1 < anchors.Count ? Math.Max(p + 1, anchors[a + 1] - 2) : content.Count;
            var description = JoinBlock(content.Skip(p + 1).Take(Math.Max(0, descEnd - (p + 1))));

            if (company is null || title is null)
                warnings.Add("Uma experiência ficou incompleta; revise empresa/cargo.");

            result.Add(new LinkedInExperienceDto(
                company ?? "(empresa a revisar)", title ?? "(cargo a revisar)",
                null, startText, endText, duration, string.IsNullOrWhiteSpace(description) ? null : description));
        }
        return result;
    }

    // ---- helpers ----

    private static List<string> SectionLines(List<string> lines, string[] header)
    {
        var start = lines.FindIndex(l => MatchesHeader(l, header));
        if (start < 0) return new();
        var block = new List<string>();
        for (var i = start + 1; i < lines.Count; i++)
        {
            if (IsAnyHeader(lines[i])) break;
            block.Add(lines[i]);
        }
        return block;
    }

    private static bool MatchesHeader(string line, string[] header)
    {
        var l = line.Trim().TrimEnd(':').ToLowerInvariant();
        return header.Any(h => l == h);
    }

    private static bool IsAnyHeader(string line) => AllHeaders.Any(h => MatchesHeader(line, h));

    private static bool IsContentLine(string line) => line.Length > 1 && !IsAnyHeader(line);

    private static bool LooksLikePeriod(string line)
    {
        var l = line.ToLowerInvariant();
        var hasRange = l.Contains('-') || l.Contains('–') || l.Contains(" a ") || l.Contains(" to ");
        var hasTime = YearRx.IsMatch(l) || l.Contains("present") || l.Contains("atual") || l.Contains("o momento")
            || MonthsPt.Any(l.Contains) || MonthsEn.Any(l.Contains);
        return hasRange && hasTime;
    }

    private static (string? Start, string? End, string? Duration) SplitPeriod(string line)
    {
        var duration = DurationRx.Match(line) is { Success: true } d ? d.Groups[1].Value.Trim() : null;
        var withoutDuration = DurationRx.Replace(line, "").Trim();
        var parts = withoutDuration.Split(new[] { '-', '–' }, 2);
        var start = parts.ElementAtOrDefault(0)?.Trim();
        var end = parts.ElementAtOrDefault(1)?.Trim();
        return (string.IsNullOrWhiteSpace(start) ? null : start, string.IsNullOrWhiteSpace(end) ? null : end, duration);
    }

    private static string JoinBlock(IEnumerable<string> lines) => string.Join(" ", lines).Trim();

    private static readonly string[] MonthsPt =
        { "janeiro", "fevereiro", "março", "marco", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };
    private static readonly string[] MonthsEn =
        { "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december",
          "jan", "feb", "mar", "apr", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
}
