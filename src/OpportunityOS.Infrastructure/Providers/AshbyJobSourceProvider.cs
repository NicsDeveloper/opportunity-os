using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Ashby public Job Board API:
/// GET https://api.ashbyhq.com/posting-api/job-board/{boardName}  (public, no auth).
/// The list already includes the full description, so no per-job detail calls.
/// Board name comes from jobs.ashbyhq.com/{boardName}.
/// </summary>
public sealed partial class AshbyJobSourceProvider : IJobSourceProvider
{
    public const string Provider = "Ashby";
    private const string ApiBase = "https://api.ashbyhq.com/posting-api/job-board";

    private readonly HttpClient _http;
    private readonly ILogger<AshbyJobSourceProvider> _logger;

    public AshbyJobSourceProvider(HttpClient http, ILogger<AshbyJobSourceProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string ProviderName => Provider;

    public bool CanHandle(Company company) => ExtractBoard(company) is not null;

    public async Task<IReadOnlyCollection<DiscoveredJobDto>> DiscoverJobsAsync(Company company, CancellationToken ct)
    {
        var board = ExtractBoard(company);
        if (board is null) return Array.Empty<DiscoveredJobDto>();

        var url = $"{ApiBase}/{board}?includeCompensation=false";
        using var response = await AtsHttp.GetWithRetryAsync(_http, url, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Ashby returned {Status} for board {Board}", (int)response.StatusCode, board);
            return Array.Empty<DiscoveredJobDto>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            return Array.Empty<DiscoveredJobDto>();

        var results = new List<DiscoveredJobDto>(jobs.GetArrayLength());
        foreach (var job in jobs.EnumerateArray())
        {
            // Only listed postings.
            if (job.TryGetProperty("isListed", out var listed) && listed.ValueKind == JsonValueKind.False) continue;

            var externalId = GetString(job, "id");
            if (string.IsNullOrEmpty(externalId)) continue;

            var title = GetString(job, "title") ?? "(sem título)";
            var location = BuildLocation(job);
            var department = GetString(job, "department");
            var html = GetString(job, "descriptionHtml");
            var text = GetString(job, "descriptionPlain") ?? AtsHttp.HtmlToText(html);
            var url2 = GetString(job, "jobUrl") ?? GetString(job, "applyUrl") ?? $"https://jobs.ashbyhq.com/{board}/{externalId}";
            var published = GetDate(job, "publishedAt");

            results.Add(new DiscoveredJobDto(
                externalId, title, company.Name, location, department,
                html, text, url2, Provider, published, published, Language: null));
        }
        return results;
    }

    internal static string? ExtractBoard(Company company)
    {
        foreach (var candidate in new[] { company.CareersUrl, company.WebsiteUrl })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var m = BoardRegex().Match(candidate);
            if (m.Success) return m.Groups["b"].Value;
        }
        return null;
    }

    private static string? BuildLocation(JsonElement job)
    {
        var remote = job.TryGetProperty("isRemote", out var r) && r.ValueKind == JsonValueKind.True;
        var loc = GetString(job, "location");
        if (!string.IsNullOrWhiteSpace(loc)) return remote ? $"{loc} (remote)" : loc!;
        return remote ? "Remote" : GetString(job, "workplaceType");
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? GetDate(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var dt)
            ? dt.ToUniversalTime() : null;

    [GeneratedRegex(@"jobs\.ashbyhq\.com/(?<b>[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BoardRegex();
}
