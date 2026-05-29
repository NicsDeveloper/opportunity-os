using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Providers;

public sealed class GoogleSearchOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string SearchEngineId { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SearchEngineId);
}

/// <summary>
/// Finds a company's ATS board using a Google Programmable Search engine. Works
/// with either engine type:
///   • Pass 1 — if a result link is itself an ATS board (e.g. an ATS-scoped engine,
///     or an indexed board on a whole-web engine), classify it and return directly.
///   • Pass 2 — otherwise take the first organic result as the official website,
///     store it, and crawl it to detect the ATS.
/// Falls back to the heuristic finder when not configured / nothing usable.
/// </summary>
public sealed class GoogleAtsBoardFinder : IAtsBoardFinder
{
    private const string Endpoint = "https://www.googleapis.com/customsearch/v1";

    private static readonly string[] Blocked =
    {
        "linkedin.", "facebook.", "instagram.", "twitter.", "x.com", "youtube.",
        "reclameaqui.", "wikipedia.", "glassdoor.", "google.", "jusbrasil.",
        "cnpj.", "econodata.", "serasa.", "gov.br"
    };

    private readonly HttpClient _http;
    private readonly GoogleSearchOptions _options;
    private readonly IAtsDetector _atsDetector;
    private readonly HeuristicAtsBoardFinder _fallback;
    private readonly ILogger<GoogleAtsBoardFinder> _logger;

    public GoogleAtsBoardFinder(
        HttpClient http, GoogleSearchOptions options, IAtsDetector atsDetector,
        HeuristicAtsBoardFinder fallback, ILogger<GoogleAtsBoardFinder> logger)
    {
        _http = http;
        _options = options;
        _atsDetector = atsDetector;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<AtsDetectionResult> FindAsync(Company company, CancellationToken ct)
    {
        if (!_options.IsConfigured)
            return await _fallback.FindAsync(company, ct);

        try
        {
            var links = await SearchAsync(company.Name, ct);

            // Pass 1: a result that is itself an ATS board.
            foreach (var link in links)
            {
                var detected = AtsDetector.Detect(link);
                if (detected is not null)
                {
                    _logger.LogInformation("CSE found {Ats} board for {Company}", detected.Ats, company.Name);
                    return detected with { CareersPageUrl = link };
                }
            }

            // Pass 2: first organic result = official website -> crawl for the ATS.
            var site = links.FirstOrDefault(l =>
                Uri.TryCreate(l, UriKind.Absolute, out var u) &&
                !Blocked.Any(b => u.Host.Contains(b, StringComparison.OrdinalIgnoreCase)));
            if (site is not null && Uri.TryCreate(site, UriKind.Absolute, out var uri))
            {
                company.SetWebsiteUrl($"{uri.Scheme}://{uri.Host}");
                _logger.LogInformation("CSE website for {Company}: {Site}", company.Name, company.WebsiteUrl);
                var crawled = await _atsDetector.DetectAsync(company, ct);
                if (crawled.Detected) return crawled;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google CSE failed; falling back to heuristic");
        }

        return await _fallback.FindAsync(company, ct);
    }

    private async Task<List<string>> SearchAsync(string companyName, CancellationToken ct)
    {
        var q = Uri.EscapeDataString($"{companyName} carreiras vagas");
        var url = $"{Endpoint}?key={_options.ApiKey}&cx={_options.SearchEngineId}&q={q}&num=6";

        var links = new List<string>();
        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google CSE returned {Status}", (int)response.StatusCode);
            return links;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var item in items.EnumerateArray())
                if (item.TryGetProperty("link", out var l) && l.GetString() is { Length: > 0 } link)
                    links.Add(link);
        return links;
    }
}
