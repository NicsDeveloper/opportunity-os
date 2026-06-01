using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Firehose raw search over Serper.dev (real Google web index). Runs the query VERBATIM
/// (no " vaga" heuristics) and returns raw organic results. Used only by the Firehose;
/// the qualified flow keeps using SerperWebJobSearchProvider.
/// </summary>
public sealed class SerperRawSearchProvider : IRawSearchProvider
{
    private const string Endpoint = "https://google.serper.dev/search";

    private readonly HttpClient _http;
    private readonly SerperSearchOptions _options;
    private readonly ILogger<SerperRawSearchProvider> _logger;

    public SerperRawSearchProvider(HttpClient http, SerperSearchOptions options, ILogger<SerperRawSearchProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public string ProviderName => "SerperRaw";
    public bool IsAvailable => _options.IsConfigured;

    private const int PerPage = 10;   // Serper returns max 10 results/request (num>10 -> 400).
    private const int MaxPages = 5;    // safety cap on pagination cost per query.

    public async Task<IReadOnlyList<RawSearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(query)) return Array.Empty<RawSearchResult>();

        var pages = Math.Clamp((int)Math.Ceiling(Math.Max(1, maxResults) / (double)PerPage), 1, MaxPages);
        var results = new List<RawSearchResult>();

        for (var page = 1; page <= pages && results.Count < maxResults; page++)
        {
            var body = new
            {
                q = query,
                num = PerPage,
                page,
                gl = "br",
                hl = "pt-br",
                tbs = _options.Freshness, // freshness window (e.g. qdr:m)
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
            req.Headers.Add("X-API-KEY", _options.ApiKey);

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Serper raw search {Status} for '{Query}' (page {Page})", (int)resp.StatusCode, query, page);
                break;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("organic", out var organic) || organic.ValueKind != JsonValueKind.Array)
                break;

            var before = results.Count;
            foreach (var item in organic.EnumerateArray())
            {
                var link = GetString(item, "link");
                if (string.IsNullOrWhiteSpace(link)) continue;
                results.Add(new RawSearchResult(
                    GetString(item, "title") ?? "(vaga)", link, GetString(item, "snippet"), null));
            }
            if (results.Count == before) break; // empty page -> stop paginating
        }

        return results.Count <= maxResults ? results : results.Take(maxResults).ToList();
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
