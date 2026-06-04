using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

public sealed class SerperSearchOptions
{
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>Hard cap on queries per SearchAsync call (protects the finite credit pool).</summary>
    public int MaxQueriesPerCall { get; set; } = 4;
    /// <summary>Google time filter for freshness. Valid: qdr:d, qdr:w, qdr:m, qdr:y. Default last month.</summary>
    public string Freshness { get; set; } = "qdr:m";
    /// <summary>Delay between queries to respect the free-tier rate limit (rapid bursts return empty).</summary>
    public int DelayMsBetweenQueries { get; set; } = 1200;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Finds recent .NET/backend job postings across the **open web** via Serper.dev
/// (Google results over a real web index — what the Google JSON API no longer does
/// for whole-web engines). Each organic result becomes an opportunity; the company is
/// derived from the result host. Freshness via Google's tbs=qdr filter. Cross-company.
/// </summary>
public sealed class SerperWebJobSearchProvider : IJobSearchProvider
{
    private const string Endpoint = "https://google.serper.dev/search";

    private static readonly string[] Blocked =
    {
        // Social / non-jobs
        "linkedin.", "facebook.", "instagram.", "twitter.", "x.com", "youtube.",
        "reclameaqui.", "wikipedia.", "google.",
        // Job aggregators (listing pages, login walls — not a specific company)
        "glassdoor.", "indeed.", "ziprecruiter.", "bebee.", "catho.", "vagas.com",
        "infojobs.", "trabalhabrasil.", "jooble.", "neuvoo.", "bne.com.br", "empregos.com",
    };

    private readonly HttpClient _http;
    private readonly SerperSearchOptions _options;
    private readonly ILogger<SerperWebJobSearchProvider> _logger;

    public SerperWebJobSearchProvider(
        HttpClient http, SerperSearchOptions options, ILogger<SerperWebJobSearchProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public string ProviderName => "SerperWeb";

    public async Task<IReadOnlyCollection<DiscoveredJobDto>> SearchAsync(
        IReadOnlyCollection<string> keywords, CancellationToken ct)
    {
        if (!_options.IsConfigured) return Array.Empty<DiscoveredJobDto>();

        var byUrl = new Dictionary<string, DiscoveredJobDto>(StringComparer.OrdinalIgnoreCase);
        var queries = keywords.Take(Math.Max(1, _options.MaxQueriesPerCall)).ToList();

        for (var i = 0; i < queries.Count; i++)
        {
            var kw = queries[i];
            // Free tier rate-limits bursts (returns empty); space out subsequent queries.
            if (i > 0 && _options.DelayMsBetweenQueries > 0)
                await Task.Delay(_options.DelayMsBetweenQueries, ct);

            // Keep the query simple: appending boolean OR clauses tanks Google results.
            var term = kw.Contains("vaga", StringComparison.OrdinalIgnoreCase) ? kw : $"{kw} vaga";
            var body = new
            {
                q = term,
                num = 10,
                gl = "br",
                hl = "pt-br",
                tbs = _options.Freshness, // e.g. qdr:m -> last month (freshness)
            };
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = JsonContent.Create(body),
                };
                req.Headers.Add("X-API-KEY", _options.ApiKey);

                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Serper web search {Status} for '{Kw}'", (int)resp.StatusCode, kw);
                    continue;
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("organic", out var organic) || organic.ValueKind != JsonValueKind.Array)
                    continue;

                int kept = 0;
                foreach (var item in organic.EnumerateArray())
                {
                    var link = GetString(item, "link");
                    if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link, UriKind.Absolute, out var uri)) continue;
                    if (Blocked.Any(b => uri.Host.Contains(b, StringComparison.OrdinalIgnoreCase))) continue;
                    kept++;

                    var title = GetString(item, "title") ?? "(vaga)";
                    byUrl[link] = new DiscoveredJobDto(
                        ExternalId: Hash(link), Title: Trim(title, 140), CompanyName: CompanyFromHost(uri.Host),
                        Location: null, Department: null, DescriptionHtml: null,
                        DescriptionText: GetString(item, "snippet") ?? title, AbsoluteUrl: link,
                        SourceProvider: "SerperWeb", PublishedAtUtc: ParseResultDate(GetString(item, "date")),
                        UpdatedAtUtc: null, Language: "pt-BR");
                }
                _logger.LogInformation("Serper '{Term}': kept {Kept} job link(s)", term, kept);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Serper web search failed for '{Kw}'", kw); }
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

    // Serper organic results carry a "date" — absolute ("Apr 15, 2026", "2026-04-15") or
    // relative ("2 days ago" / "há 3 dias"). Parse it into a real publish date when we can.
    private static DateTime? ParseResultDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var abs))
            return abs <= DateTime.UtcNow.AddDays(1) ? abs : null;

        var m = Regex.Match(s, @"(\d+)\s*(hour|hora|day|dia|week|semana|month|m[eê]s|mes|year|ano)",
            RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var n = int.Parse(m.Groups[1].Value);
        var u = m.Groups[2].Value.ToLowerInvariant();
        var now = DateTime.UtcNow;
        if (u.StartsWith("hour") || u.StartsWith("hora")) return now.AddHours(-n);
        if (u.StartsWith("day") || u.StartsWith("dia")) return now.AddDays(-n);
        if (u.StartsWith("week") || u.StartsWith("semana")) return now.AddDays(-7 * n);
        if (u.StartsWith("year") || u.StartsWith("ano")) return now.AddYears(-n);
        return now.AddMonths(-n); // month variants
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];

    private static string Hash(string s) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)))[..16].ToLowerInvariant();
}
