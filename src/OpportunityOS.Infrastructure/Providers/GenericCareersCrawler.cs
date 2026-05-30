using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Discovers jobs straight from a company's own website — visit the site, find the
/// "Carreiras / Trabalhe Conosco" page, and extract job-posting links by their role
/// text. No ATS API required: this is how we go beyond Greenhouse/Lever/Gupy and
/// reach the long tail of companies.
///
/// Honest limits: pure HTTP (no JS rendering), so sites that render their job board
/// client-side won't yield here (those need a headless browser later). Descriptions
/// aren't fetched (title-only); good enough to score and link, fast and robust.
/// </summary>
public sealed partial class GenericCareersCrawler : IJobSourceProvider
{
    public const string Provider = "CareersCrawler";
    private const int MaxPages = 5;
    private const int MaxJobs = 60;

    // ATS hosts handled by dedicated providers — don't double-emit from here.
    private static readonly string[] AtsHosts =
    {
        "greenhouse.io", "lever.co", "gupy.io", "smartrecruiters.com", "ashbyhq.com",
        "myworkdayjobs.com", "recruitee.com", "workable.com", "teamtailor.com",
    };

    private readonly HttpClient _http;
    private readonly ILogger<GenericCareersCrawler> _logger;

    public GenericCareersCrawler(HttpClient http, ILogger<GenericCareersCrawler> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string ProviderName => Provider;

    // Crawl any company that has a website we can fetch.
    public bool CanHandle(Company company) => !string.IsNullOrWhiteSpace(company.WebsiteUrl);

    public async Task<IReadOnlyCollection<DiscoveredJobDto>> DiscoverJobsAsync(Company company, CancellationToken ct)
    {
        var start = company.WebsiteUrl;
        if (string.IsNullOrWhiteSpace(start) || !Uri.TryCreate(start, UriKind.Absolute, out var baseUri))
            return Array.Empty<DiscoveredJobDto>();

        var companyHost = baseUri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        var queue = new List<string> { start };
        if (!string.IsNullOrWhiteSpace(company.CareersUrl)) queue.Add(company.CareersUrl!);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var byUrl = new Dictionary<string, DiscoveredJobDto>(StringComparer.OrdinalIgnoreCase);
        var fetches = 0;

        for (var i = 0; i < queue.Count && fetches < MaxPages && byUrl.Count < MaxJobs; i++)
        {
            var url = queue[i];
            if (!visited.Add(url)) continue;
            var html = await TryFetchAsync(url, ct);
            fetches++;
            if (html is null) continue;

            // Queue careers-looking links (one level deep) to reach the jobs page.
            foreach (var link in AtsDetector.ExtractCareersLinks(html, url))
                if (!visited.Contains(link) && !queue.Contains(link) && SameCompany(link, companyHost)) queue.Add(link);

            // Extract job-posting links by role text.
            foreach (var (href, text) in ExtractAnchors(html, url))
            {
                if (!RoleRegex().IsMatch(text) && !RoleRegex().IsMatch(href)) continue;
                if (!Uri.TryCreate(href, UriKind.Absolute, out var jobUri)) continue;
                if (IsAtsHost(jobUri.Host)) continue;              // handled by ATS providers
                if (!SameCompany(jobUri.ToString(), companyHost)) continue;

                var title = Clean(text);
                if (title.Length < 4 || title.Length > 120) continue;
                var abs = jobUri.ToString();
                byUrl[abs] = new DiscoveredJobDto(
                    ExternalId: Hash(abs), Title: title, CompanyName: company.Name,
                    Location: null, Department: null, DescriptionHtml: null,
                    DescriptionText: title, AbsoluteUrl: abs, SourceProvider: Provider,
                    PublishedAtUtc: null, UpdatedAtUtc: null, Language: null);
                if (byUrl.Count >= MaxJobs) break;
            }
        }

        if (byUrl.Count > 0)
            _logger.LogInformation("CareersCrawler: {Count} jobs from {Company}", byUrl.Count, company.Name);
        return byUrl.Values.ToList();
    }

    private static bool IsAtsHost(string host) =>
        AtsHosts.Any(a => host.Contains(a, StringComparison.OrdinalIgnoreCase));

    private static bool SameCompany(string url, string companyHost)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        var host = u.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        return host.Equals(companyHost, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + companyHost, StringComparison.OrdinalIgnoreCase)
            || companyHost.EndsWith("." + host, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Href, string Text)> ExtractAnchors(string html, string baseUrl)
    {
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var b);
        foreach (Match m in AnchorRegex().Matches(html))
        {
            var href = m.Groups["href"].Value;
            var text = m.Groups["text"].Value;
            if (string.IsNullOrWhiteSpace(href)) continue;
            string abs;
            if (Uri.TryCreate(href, UriKind.Absolute, out var a)) abs = a.ToString();
            else if (b is not null && Uri.TryCreate(b, href, out var rel)) abs = rel.ToString();
            else continue;
            yield return (abs, text);
        }
    }

    private async Task<string?> TryFetchAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await AtsHttp.GetWithRetryAsync(_http, url, ct);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync(ct) : null;
        }
        catch (Exception ex) { _logger.LogDebug(ex, "CareersCrawler fetch failed {Url}", url); return null; }
    }

    private static string Clean(string text) =>
        WhitespaceRegex().Replace(TagRegex().Replace(System.Net.WebUtility.HtmlDecode(text), " "), " ").Trim();

    private static string Hash(string s)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    [GeneratedRegex(@"<a\s+[^>]*href\s*=\s*[""'](?<href>[^""']+)[""'][^>]*>(?<text>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"desenvolvedor|programador|engenheiro|developer|engineer|\.net|c#|csharp|back[\s-]?end|software (engineer|developer)|full[\s-]?stack|dev\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex RoleRegex();

    [GeneratedRegex("<[^>]+>")] private static partial Regex TagRegex();
    [GeneratedRegex(@"\s{2,}")] private static partial Regex WhitespaceRegex();
}
