using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Detects a company's ATS from text (a page's HTML, or a board URL). Recognizes
/// many ATS — but only Greenhouse/Lever/Gupy are <c>ProviderSupported</c> (we can
/// fetch their jobs today); the rest are detected/linked until a provider exists.
/// </summary>
public sealed class AtsDetector : IAtsDetector
{
    private const int MaxPages = 4;

    private readonly HttpClient _http;
    private readonly ILogger<AtsDetector> _logger;

    public AtsDetector(HttpClient http, ILogger<AtsDetector> logger)
    {
        _http = http;
        _logger = logger;
    }

    // Ordered: token-canonical (fetchable) first, then host-based (detect/link only).
    private sealed record Spec(string Ats, bool Supported, Regex Rx, Func<Match, string> Board, string TokenGroup);

    private static readonly Spec[] Specs =
    {
        new("Greenhouse", true,  Rx(@"(?:job-)?boards\.greenhouse\.io/(?<t>[A-Za-z0-9_-]+)"),
            m => $"https://boards.greenhouse.io/{m.Groups["t"].Value}", "t"),
        new("Lever", true,       Rx(@"jobs\.lever\.co/(?<t>[A-Za-z0-9_.-]+)"),
            m => $"https://jobs.lever.co/{m.Groups["t"].Value}", "t"),
        new("Gupy", true,        Rx(@"(?<t>[A-Za-z0-9-]+)\.gupy\.io"),
            m => $"https://{m.Groups["t"].Value}.gupy.io", "t"),
        new("Workday", false,    Rx(@"(?<h>[A-Za-z0-9-]+\.(?:wd\d+\.)?myworkdayjobs\.com)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Ashby", false,      Rx(@"jobs\.ashbyhq\.com/(?<t>[A-Za-z0-9_-]+)"),
            m => $"https://jobs.ashbyhq.com/{m.Groups["t"].Value}", "t"),
        new("SmartRecruiters", false, Rx(@"(?<h>(?:jobs|careers)\.smartrecruiters\.com)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Workable", false,   Rx(@"(?<t>[A-Za-z0-9_-]+)\.workable\.com"),
            m => $"https://{m.Groups["t"].Value}.workable.com", "t"),
        new("Recruitee", false,  Rx(@"(?<t>[A-Za-z0-9_-]+)\.recruitee\.com"),
            m => $"https://{m.Groups["t"].Value}.recruitee.com", "t"),
        new("Teamtailor", false, Rx(@"(?<t>[A-Za-z0-9_-]+)\.teamtailor\.com"),
            m => $"https://{m.Groups["t"].Value}.teamtailor.com", "t"),
        new("Breezy", false,     Rx(@"(?<t>[A-Za-z0-9_-]+)\.breezy\.hr"),
            m => $"https://{m.Groups["t"].Value}.breezy.hr", "t"),
        // Brazil
        new("inhire", false,     Rx(@"(?<t>[A-Za-z0-9_-]+)\.inhire\.(?:app|io)"),
            m => $"https://{m.Groups["t"].Value}.inhire.app", "t"),
        new("Abler", false,      Rx(@"(?<t>[A-Za-z0-9_-]+)\.abler\.com\.br"),
            m => $"https://{m.Groups["t"].Value}.abler.com.br", "t"),
        new("Solides", false,    Rx(@"(?<h>[A-Za-z0-9_.-]+\.solides\.com\.br)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Pandape", false,    Rx(@"(?<h>[A-Za-z0-9_.-]+\.pandape\.com(?:\.br)?)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Kenoby", false,     Rx(@"(?<h>[A-Za-z0-9_.-]+\.kenoby\.com)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Quickin", false,    Rx(@"(?<h>[A-Za-z0-9_.-]+\.quickin\.io)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("JobConvo", false,   Rx(@"(?<h>[A-Za-z0-9_.-]+\.jobconvo\.com)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Taqe", false,       Rx(@"(?<h>[A-Za-z0-9_.-]+\.taqe\.com\.br)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("99jobs", false,     Rx(@"(?<h>[A-Za-z0-9_.-]+\.99jobs\.com)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Recrutei", false,   Rx(@"(?<h>[A-Za-z0-9_.-]+\.recrutei\.com\.br)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("GeekHunter", false, Rx(@"(?<h>(?:www\.)?geekhunter\.com\.br)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Coodesh", false,    Rx(@"(?<h>(?:www\.)?coodesh\.com)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
        new("Programathor", false, Rx(@"(?<h>(?:www\.)?programathor\.com\.br)"),
            m => $"https://{m.Groups["h"].Value}", "h"),
    };

    private static Regex Rx(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<AtsDetectionResult> DetectAsync(Company company, CancellationToken ct)
    {
        var queue = new List<string>();
        if (!string.IsNullOrWhiteSpace(company.CareersUrl)) queue.Add(company.CareersUrl!);
        if (!string.IsNullOrWhiteSpace(company.WebsiteUrl)) queue.Add(company.WebsiteUrl!);
        if (queue.Count == 0) return NotDetected();

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? careersFound = null;
        var fetches = 0;

        for (var i = 0; i < queue.Count && fetches < MaxPages; i++)
        {
            var url = queue[i];
            if (!visited.Add(url)) continue;

            var html = await TryFetchAsync(url, ct);
            fetches++;
            if (html is null) continue;

            var hit = Detect(html);
            if (hit is not null) return hit with { CareersPageUrl = careersFound ?? url };

            foreach (var link in ExtractCareersLinks(html, url))
            {
                careersFound ??= link;
                if (!visited.Contains(link) && !queue.Contains(link)) queue.Add(link);
            }
        }
        return NotDetected() with { CareersPageUrl = careersFound };
    }

    /// <summary>Classify a chunk of text (page HTML or a board URL) by ATS host pattern.</summary>
    public static AtsDetectionResult? Detect(string text)
    {
        foreach (var spec in Specs)
        {
            var m = spec.Rx.Match(text);
            if (!m.Success) continue;
            var token = m.Groups[spec.TokenGroup].Success ? m.Groups[spec.TokenGroup].Value : null;
            return new AtsDetectionResult(true, spec.Ats, spec.Board(m), token, null, spec.Supported);
        }
        return null;
    }

    public static IEnumerable<string> ExtractCareersLinks(string html, string baseUrl)
    {
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var b);
        foreach (Match m in HrefRegex.Matches(html))
        {
            var href = m.Groups["href"].Value;
            if (string.IsNullOrWhiteSpace(href) || !CareersTermRegex.IsMatch(href)) continue;
            if (Uri.TryCreate(href, UriKind.Absolute, out var abs)) yield return abs.ToString();
            else if (b is not null && Uri.TryCreate(b, href, out var rel)) yield return rel.ToString();
        }
    }

    private async Task<string?> TryFetchAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await AtsHttp.GetWithRetryAsync(_http, url, ct);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ATS detect: failed to fetch {Url}", url);
            return null;
        }
    }

    private static AtsDetectionResult NotDetected() => new(false, null, null, null, null, false);

    private static readonly Regex HrefRegex =
        new("href\\s*=\\s*[\"'](?<href>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CareersTermRegex =
        new(@"carreira|career|careers|jobs|vagas|trabalhe|join-?us|oportunidades|positions",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
