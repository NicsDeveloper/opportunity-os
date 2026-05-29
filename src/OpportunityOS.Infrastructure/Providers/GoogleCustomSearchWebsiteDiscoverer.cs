using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

public sealed class GoogleSearchOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string SearchEngineId { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SearchEngineId);
}

/// <summary>
/// Website discovery via the Google Programmable Search (Custom Search JSON API):
/// query "{company} site oficial", skip social/aggregator hosts, and return the
/// first organic result's root URL. Falls back to the heuristic discoverer when
/// Google returns nothing or errors.
/// </summary>
public sealed class GoogleCustomSearchWebsiteDiscoverer : ICompanyWebsiteDiscoverer
{
    private const string Endpoint = "https://www.googleapis.com/customsearch/v1";

    // Hosts that are never the company's own careers/official site.
    private static readonly string[] Blocked =
    {
        "linkedin.", "facebook.", "instagram.", "twitter.", "x.com", "youtube.",
        "reclameaqui.", "wikipedia.", "glassdoor.", "google.", "gov.br", "jusbrasil.",
        "cnpj.", "econodata.", "serasa."
    };

    private readonly HttpClient _http;
    private readonly GoogleSearchOptions _options;
    private readonly CompanyWebsiteDiscoverer _fallback;
    private readonly ILogger<GoogleCustomSearchWebsiteDiscoverer> _logger;

    public GoogleCustomSearchWebsiteDiscoverer(
        HttpClient http, GoogleSearchOptions options,
        CompanyWebsiteDiscoverer fallback, ILogger<GoogleCustomSearchWebsiteDiscoverer> logger)
    {
        _http = http;
        _options = options;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<WebsiteDiscoveryResult> DiscoverAsync(string companyName, CancellationToken ct)
    {
        if (!_options.IsConfigured)
            return await _fallback.DiscoverAsync(companyName, ct);

        try
        {
            var q = Uri.EscapeDataString($"{companyName} site oficial");
            var url = $"{Endpoint}?key={_options.ApiKey}&cx={_options.SearchEngineId}&q={q}&num=5";

            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google CSE returned {Status}; falling back", (int)response.StatusCode);
                return await _fallback.DiscoverAsync(companyName, ct);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var link = item.TryGetProperty("link", out var l) ? l.GetString() : null;
                    if (string.IsNullOrWhiteSpace(link)) continue;
                    if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)) continue;
                    if (Blocked.Any(b => uri.Host.Contains(b, StringComparison.OrdinalIgnoreCase))) continue;

                    var root = $"{uri.Scheme}://{uri.Host}";
                    _logger.LogInformation("Google CSE found {Url} for {Company}", root, companyName);
                    return new WebsiteDiscoveryResult(true, root);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google CSE failed; falling back to heuristic");
        }

        return await _fallback.DiscoverAsync(companyName, ct);
    }
}
