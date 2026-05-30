using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Website discovery via Google Programmable Search (whole-web engine): query the
/// company name, return the first organic non-blocked result's root URL. Falls back
/// to the heuristic domain-probing discoverer when not configured / nothing usable.
/// Used both as the onboarding fallback and by the website backfill (for logos).
/// </summary>
public sealed class GoogleWebsiteDiscoverer : ICompanyWebsiteDiscoverer
{
    private const string Endpoint = "https://www.googleapis.com/customsearch/v1";

    private static readonly string[] Blocked =
    {
        "linkedin.", "facebook.", "instagram.", "twitter.", "x.com", "youtube.",
        "reclameaqui.", "wikipedia.", "glassdoor.", "google.", "jusbrasil.",
        "cnpj.", "econodata.", "serasa.", "gov.br", "gupy.io", "greenhouse.io",
        "lever.co", "ashbyhq.com", "smartrecruiters.com",
    };

    private readonly HttpClient _http;
    private readonly GoogleSearchOptions _options;
    private readonly GoogleQuotaGuard _quota;
    private readonly CompanyWebsiteDiscoverer _fallback;
    private readonly ILogger<GoogleWebsiteDiscoverer> _logger;

    public GoogleWebsiteDiscoverer(
        HttpClient http, GoogleSearchOptions options, GoogleQuotaGuard quota,
        CompanyWebsiteDiscoverer fallback, ILogger<GoogleWebsiteDiscoverer> logger)
    {
        _http = http;
        _options = options;
        _quota = quota;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<WebsiteDiscoveryResult> DiscoverAsync(string companyName, CancellationToken ct)
    {
        if (!_options.IsConfigured || !_quota.TryConsume())
            return await _fallback.DiscoverAsync(companyName, ct);

        try
        {
            var q = Uri.EscapeDataString($"{companyName} site oficial");
            var url = $"{Endpoint}?key={_options.ApiKey}&cx={_options.SearchEngineId}&q={q}&num=5";
            using var response = await _http.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var link = item.TryGetProperty("link", out var l) ? l.GetString() : null;
                        if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link, UriKind.Absolute, out var uri)) continue;
                        if (Blocked.Any(b => uri.Host.Contains(b, StringComparison.OrdinalIgnoreCase))) continue;
                        return new WebsiteDiscoveryResult(true, $"{uri.Scheme}://{uri.Host}");
                    }
                }
            }
            else
            {
                _logger.LogWarning("Google CSE (website) returned {Status}", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google website discovery failed; using heuristic");
        }
        return await _fallback.DiscoverAsync(companyName, ct);
    }
}
