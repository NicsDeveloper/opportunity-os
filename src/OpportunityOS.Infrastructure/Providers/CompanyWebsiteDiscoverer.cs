using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Heuristic website discovery: derive candidate domains from the company name
/// (brand slug + common TLDs), probe each, and accept the first that responds 200
/// AND whose page mentions the brand token (to avoid parked/wrong domains).
///
/// This is intentionally conservative — a miss is better than a wrong site. For
/// higher recall, a Google Custom Search strategy can be added behind this same
/// interface when an API key is configured.
/// </summary>
public sealed class CompanyWebsiteDiscoverer : ICompanyWebsiteDiscoverer
{
    private const int MaxCandidates = 6;

    private readonly HttpClient _http;
    private readonly ILogger<CompanyWebsiteDiscoverer> _logger;

    public CompanyWebsiteDiscoverer(HttpClient http, ILogger<CompanyWebsiteDiscoverer> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<WebsiteDiscoveryResult> DiscoverAsync(string companyName, CancellationToken ct)
    {
        var token = CompanyNameNormalizer.PrimaryToken(companyName);
        if (string.IsNullOrWhiteSpace(token))
            return new WebsiteDiscoveryResult(false, null);

        var candidates = CompanyNameNormalizer.CandidateUrls(companyName).Take(MaxCandidates);
        foreach (var url in candidates)
        {
            try
            {
                using var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) continue;

                var body = await response.Content.ReadAsStringAsync(ct);
                // Compact the page (drop spaces/punctuation) so "BTG Pactual" matches slug "btgpactual".
                var head = body.Length > 8000 ? body[..8000] : body;
                var compact = new string(head.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                if (compact.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    var resolved = response.RequestMessage?.RequestUri?.ToString() ?? url;
                    _logger.LogInformation("Website discovered for {Company}: {Url}", companyName, resolved);
                    return new WebsiteDiscoveryResult(true, resolved);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Candidate {Url} failed", url);
            }
        }
        return new WebsiteDiscoveryResult(false, null);
    }
}
