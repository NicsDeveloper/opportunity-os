using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Finds recent .NET/backend job postings across the **open web** via Google CSE
/// (dateRestrict for freshness). Cross-company: each result becomes an opportunity,
/// the company is derived from the result host. Quota-guarded (shared daily budget),
/// capped per call so it never blows the 100/day free tier.
/// </summary>
public sealed class GoogleWebJobSearchProvider : IJobSearchProvider
{
    private const string Endpoint = "https://www.googleapis.com/customsearch/v1";
    private const int MaxQueriesPerCall = 4;

    private static readonly string[] Blocked =
    {
        "linkedin.", "facebook.", "instagram.", "twitter.", "x.com", "youtube.",
        "reclameaqui.", "wikipedia.", "glassdoor.", "indeed.", "google.",
    };

    private readonly HttpClient _http;
    private readonly GoogleSearchOptions _options;
    private readonly GoogleQuotaGuard _quota;
    private readonly ILogger<GoogleWebJobSearchProvider> _logger;

    public GoogleWebJobSearchProvider(
        HttpClient http, GoogleSearchOptions options, GoogleQuotaGuard quota,
        ILogger<GoogleWebJobSearchProvider> logger)
    {
        _http = http;
        _options = options;
        _quota = quota;
        _logger = logger;
    }

    public string ProviderName => "GoogleWeb";

    public async Task<IReadOnlyCollection<DiscoveredJobDto>> SearchAsync(
        IReadOnlyCollection<string> keywords, CancellationToken ct)
    {
        if (!_options.IsConfigured) return Array.Empty<DiscoveredJobDto>();

        var byUrl = new Dictionary<string, DiscoveredJobDto>(StringComparer.OrdinalIgnoreCase);
        var queries = keywords.Take(MaxQueriesPerCall);

        foreach (var kw in queries)
        {
            if (!_quota.TryConsume())
            {
                _logger.LogInformation("Google quota exhausted for today; skipping web search.");
                break;
            }

            var q = Uri.EscapeDataString($"{kw} (vaga OR vagas OR carreira OR \"trabalhe conosco\")");
            // dateRestrict=m3 -> only results from the last 3 months (freshness).
            var url = $"{Endpoint}?key={_options.ApiKey}&cx={_options.SearchEngineId}&q={q}&num=10&dateRestrict=m3";
            try
            {
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) { _logger.LogWarning("Google web search {Status}", (int)resp.StatusCode); continue; }
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in items.EnumerateArray())
                {
                    var link = GetString(item, "link");
                    if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link, UriKind.Absolute, out var uri)) continue;
                    if (Blocked.Any(b => uri.Host.Contains(b, StringComparison.OrdinalIgnoreCase))) continue;

                    var title = GetString(item, "title") ?? "(vaga)";
                    byUrl[link] = new DiscoveredJobDto(
                        ExternalId: Hash(link), Title: Trim(title, 140), CompanyName: CompanyFromHost(uri.Host),
                        Location: null, Department: null, DescriptionHtml: null,
                        DescriptionText: GetString(item, "snippet") ?? title, AbsoluteUrl: link,
                        SourceProvider: "GoogleWeb", PublishedAtUtc: null, UpdatedAtUtc: null, Language: "pt-BR");
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Google web search failed for '{Kw}'", kw); }
        }
        return byUrl.Values.ToList();
    }

    private static string CompanyFromHost(string host)
    {
        var parts = host.Replace("www.", "", StringComparison.OrdinalIgnoreCase)
            .Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        string[] subs = { "careers", "carreiras", "jobs", "vagas", "boards", "job-boards", "trabalheconosco" };
        while (parts.Count > 2 && subs.Contains(parts[0], StringComparer.OrdinalIgnoreCase)) parts.RemoveAt(0);
        var label = parts.Count > 0 ? parts[0] : host;
        return char.ToUpperInvariant(label[0]) + label[1..];
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];

    private static string Hash(string s) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)))[..16].ToLowerInvariant();
}
