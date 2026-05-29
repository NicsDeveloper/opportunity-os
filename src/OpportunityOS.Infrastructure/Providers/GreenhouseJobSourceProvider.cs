using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Greenhouse public Job Board API:
/// GET https://boards-api.greenhouse.io/v1/boards/{token}/jobs?content=true
/// (public, no auth). Board token is derived from the company's careers URL.
/// </summary>
public sealed partial class GreenhouseJobSourceProvider : IJobSourceProvider
{
    public const string Provider = "Greenhouse";
    private const string ApiBase = "https://boards-api.greenhouse.io/v1/boards";

    private readonly HttpClient _http;
    private readonly ILogger<GreenhouseJobSourceProvider> _logger;

    public GreenhouseJobSourceProvider(HttpClient http, ILogger<GreenhouseJobSourceProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string ProviderName => Provider;

    public bool CanHandle(Company company) => ExtractBoardToken(company) is not null;

    public async Task<IReadOnlyCollection<DiscoveredJobDto>> DiscoverJobsAsync(Company company, CancellationToken ct)
    {
        var token = ExtractBoardToken(company);
        if (token is null) return Array.Empty<DiscoveredJobDto>();

        var url = $"{ApiBase}/{token}/jobs?content=true";
        using var response = await AtsHttp.GetWithRetryAsync(_http, url, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Greenhouse returned {Status} for board {Token}", (int)response.StatusCode, token);
            return Array.Empty<DiscoveredJobDto>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            return Array.Empty<DiscoveredJobDto>();

        var results = new List<DiscoveredJobDto>(jobs.GetArrayLength());
        foreach (var job in jobs.EnumerateArray())
        {
            var externalId = job.TryGetProperty("id", out var idEl)
                ? idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64().ToString() : idEl.GetString() ?? ""
                : "";
            if (string.IsNullOrEmpty(externalId)) continue;

            var title = GetString(job, "title") ?? "(sem título)";
            var absoluteUrl = GetString(job, "absolute_url") ?? $"https://boards.greenhouse.io/{token}/jobs/{externalId}";
            var location = job.TryGetProperty("location", out var loc) ? GetString(loc, "name") : null;
            var department = ReadFirstName(job, "departments");
            var html = GetString(job, "content");
            var updatedAt = GetDate(job, "updated_at");

            results.Add(new DiscoveredJobDto(
                externalId, title, company.Name, location, department,
                html, AtsHttp.HtmlToText(html), absoluteUrl, Provider,
                PublishedAtUtc: null, UpdatedAtUtc: updatedAt, Language: null));
        }

        return results;
    }

    /// <summary>Token from boards.greenhouse.io/{token} or job-boards.greenhouse.io/{token}.</summary>
    internal static string? ExtractBoardToken(Company company)
    {
        foreach (var candidate in new[] { company.CareersUrl, company.WebsiteUrl })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var m = TokenRegex().Match(candidate);
            if (m.Success) return m.Groups["token"].Value;
        }
        return null;
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? GetDate(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var dt)
            ? dt.ToUniversalTime() : null;

    private static string? ReadFirstName(JsonElement el, string arrayProp)
    {
        if (el.TryGetProperty(arrayProp, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var item in arr.EnumerateArray())
                if (GetString(item, "name") is { Length: > 0 } name)
                    return name;
        return null;
    }

    [GeneratedRegex(@"(?:job-)?boards\.greenhouse\.io/(?<token>[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();
}
