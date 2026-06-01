using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Fetches a job page and extracts visible text. Tries a plain HTTP GET first (cheap);
/// if the static HTML yields too little text (likely a JS-rendered SPA) and a renderer is
/// available, falls back to Playwright. Output is truncated to keep scoring cheap.
/// </summary>
public sealed partial class HtmlJobContentEnricher : IJobContentEnricher
{
    private const int MaxChars = 6000;
    private const int MinUsefulChars = 400;

    private readonly HttpClient _http;
    private readonly IPageRenderer _renderer;
    private readonly ILogger<HtmlJobContentEnricher> _logger;

    public HtmlJobContentEnricher(HttpClient http, IPageRenderer renderer, ILogger<HtmlJobContentEnricher> logger)
    {
        _http = http;
        _renderer = renderer;
        _logger = logger;
    }

    public async Task<string?> FetchTextAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return null;
        try
        {
            string? html = null;
            using (var resp = await _http.GetAsync(url, ct))
            {
                if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return null;
                if (resp.IsSuccessStatusCode) html = await resp.Content.ReadAsStringAsync(ct);
            }

            var text = ToText(html);
            // SPA fallback: static HTML had little usable text and a browser is available.
            if (text.Length < MinUsefulChars && _renderer.IsAvailable)
            {
                var rendered = await _renderer.RenderAsync(url, ct);
                var renderedText = ToText(rendered);
                if (renderedText.Length > text.Length) text = renderedText;
            }

            return string.IsNullOrWhiteSpace(text) ? null : Truncate(text, MaxChars);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Content enrichment failed for {Url}", url);
            return null;
        }
    }

    private static string ToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var noScript = ScriptStyleRegex().Replace(html, " ");
        var noTags = TagRegex().Replace(noScript, " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"<(script|style)[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
