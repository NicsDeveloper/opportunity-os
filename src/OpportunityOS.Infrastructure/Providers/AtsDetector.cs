using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Detects a company's ATS by inspecting its public website and careers page.
/// Strategy: scan the given URLs' HTML for known ATS host patterns; if none,
/// follow up to a few "careers"-looking links one level deep and scan those.
/// </summary>
public sealed partial class AtsDetector : IAtsDetector
{
    private const int MaxPages = 4;

    private readonly HttpClient _http;
    private readonly ILogger<AtsDetector> _logger;

    public AtsDetector(HttpClient http, ILogger<AtsDetector> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<AtsDetectionResult> DetectAsync(Company company, CancellationToken ct)
    {
        var queue = new List<string>();
        if (!string.IsNullOrWhiteSpace(company.CareersUrl)) queue.Add(company.CareersUrl!);
        if (!string.IsNullOrWhiteSpace(company.WebsiteUrl)) queue.Add(company.WebsiteUrl!);
        if (queue.Count == 0)
            return NotDetected();

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var careersFound = (string?)null;
        var fetches = 0;

        for (var i = 0; i < queue.Count && fetches < MaxPages; i++)
        {
            var url = queue[i];
            if (!visited.Add(url)) continue;

            var html = await TryFetchAsync(url, ct);
            fetches++;
            if (html is null) continue;

            // 1) Direct ATS detection on this page.
            var hit = Detect(html);
            if (hit is not null)
                return hit with { CareersPageUrl = careersFound ?? url };

            // 2) Otherwise queue careers-looking links (one level deep).
            foreach (var link in ExtractCareersLinks(html, url))
            {
                careersFound ??= link;
                if (!visited.Contains(link) && !queue.Contains(link)) queue.Add(link);
            }
        }

        return NotDetected() with { CareersPageUrl = careersFound };
    }

    /// <summary>Scan HTML for the first known ATS host pattern.</summary>
    public static AtsDetectionResult? Detect(string html)
    {
        var gh = GreenhouseRegex().Match(html);
        if (gh.Success)
        {
            var token = gh.Groups["token"].Value;
            return new AtsDetectionResult(true, "Greenhouse", $"https://boards.greenhouse.io/{token}", token, null, ProviderSupported: true);
        }
        var lv = LeverRegex().Match(html);
        if (lv.Success)
        {
            var token = lv.Groups["token"].Value;
            return new AtsDetectionResult(true, "Lever", $"https://jobs.lever.co/{token}", token, null, ProviderSupported: true);
        }
        var gp = GupyRegex().Match(html);
        if (gp.Success)
        {
            var token = gp.Groups["token"].Value;
            // Gupy discovery is keyword-based (cross-company), but record the board.
            return new AtsDetectionResult(true, "Gupy", $"https://{token}.gupy.io", token, null, ProviderSupported: true);
        }
        var wd = WorkdayRegex().Match(html);
        if (wd.Success)
        {
            var token = wd.Groups["token"].Value;
            return new AtsDetectionResult(true, "Workday", wd.Value.StartsWith("http") ? wd.Value : $"https://{wd.Value}", token, null, ProviderSupported: false);
        }
        return null;
    }

    public static IEnumerable<string> ExtractCareersLinks(string html, string baseUrl)
    {
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var b);
        foreach (Match m in HrefRegex().Matches(html))
        {
            var href = m.Groups["href"].Value;
            if (string.IsNullOrWhiteSpace(href)) continue;
            if (!CareersTermRegex().IsMatch(href)) continue;

            if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
                yield return abs.ToString();
            else if (b is not null && Uri.TryCreate(b, href, out var rel))
                yield return rel.ToString();
        }
    }

    private async Task<string?> TryFetchAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await AtsHttp.GetWithRetryAsync(_http, url, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ATS detect: failed to fetch {Url}", url);
            return null;
        }
    }

    private static AtsDetectionResult NotDetected() =>
        new(false, null, null, null, null, false);

    [GeneratedRegex(@"(?:job-)?boards\.greenhouse\.io/(?<token>[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex GreenhouseRegex();

    [GeneratedRegex(@"jobs\.lever\.co/(?<token>[A-Za-z0-9_.-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LeverRegex();

    [GeneratedRegex(@"(?<token>[A-Za-z0-9-]+)\.gupy\.io", RegexOptions.IgnoreCase)]
    private static partial Regex GupyRegex();

    [GeneratedRegex(@"(?<token>[A-Za-z0-9-]+\.(?:wd\d+\.)?myworkdayjobs\.com)", RegexOptions.IgnoreCase)]
    private static partial Regex WorkdayRegex();

    [GeneratedRegex("href\\s*=\\s*[\"'](?<href>[^\"']+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    [GeneratedRegex(@"carreira|career|careers|jobs|vagas|trabalhe|join-?us|oportunidades|positions", RegexOptions.IgnoreCase)]
    private static partial Regex CareersTermRegex();
}
