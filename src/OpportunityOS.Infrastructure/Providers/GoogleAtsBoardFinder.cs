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
/// Finds a company's ATS board via a Google Programmable Search engine that is
/// **scoped to ATS domains** (greenhouse.io, lever.co, gupy.io, inhire.app, ...).
/// A query for the company name returns its board directly; we classify the
/// result link with <see cref="AtsDetector.Detect"/>. Falls back to the heuristic
/// finder when not configured / no usable result.
///
/// (Google deprecated "search the entire web" for new engines on 2026-01-20, so we
/// scope the engine to ATS domains instead — which also goes straight to the board.)
/// </summary>
public sealed class GoogleAtsBoardFinder : IAtsBoardFinder
{
    private const string Endpoint = "https://www.googleapis.com/customsearch/v1";

    private readonly HttpClient _http;
    private readonly GoogleSearchOptions _options;
    private readonly HeuristicAtsBoardFinder _fallback;
    private readonly ILogger<GoogleAtsBoardFinder> _logger;

    public GoogleAtsBoardFinder(
        HttpClient http, GoogleSearchOptions options,
        HeuristicAtsBoardFinder fallback, ILogger<GoogleAtsBoardFinder> logger)
    {
        _http = http;
        _options = options;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<AtsDetectionResult> FindAsync(Company company, CancellationToken ct)
    {
        if (!_options.IsConfigured)
            return await _fallback.FindAsync(company, ct);

        try
        {
            var q = Uri.EscapeDataString(company.Name);
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
                        if (string.IsNullOrWhiteSpace(link)) continue;

                        var detected = AtsDetector.Detect(link);
                        if (detected is not null)
                        {
                            _logger.LogInformation("CSE-ATS found {Ats} board for {Company}: {Url}",
                                detected.Ats, company.Name, detected.BoardUrl);
                            return detected with { CareersPageUrl = link };
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("Google CSE returned {Status}; falling back", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google CSE failed; falling back to heuristic");
        }

        return await _fallback.FindAsync(company, ct);
    }
}
