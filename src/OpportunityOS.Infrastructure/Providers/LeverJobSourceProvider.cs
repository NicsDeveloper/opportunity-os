using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Lever public Postings API:
/// GET https://api.lever.co/v0/postings/{handle}?mode=json (public).
/// Handle is derived from the company's jobs.lever.co/{handle} URL.
/// </summary>
public sealed partial class LeverJobSourceProvider : IJobSourceProvider
{
    public const string Provider = "Lever";
    private const string ApiBase = "https://api.lever.co/v0/postings";

    private readonly HttpClient _http;
    private readonly ILogger<LeverJobSourceProvider> _logger;

    public LeverJobSourceProvider(HttpClient http, ILogger<LeverJobSourceProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string ProviderName => Provider;

    public bool CanHandle(Company company) => ExtractHandle(company) is not null;

    public async Task<IReadOnlyCollection<DiscoveredJobDto>> DiscoverJobsAsync(Company company, CancellationToken ct)
    {
        var handle = ExtractHandle(company);
        if (handle is null) return Array.Empty<DiscoveredJobDto>();

        var url = $"{ApiBase}/{handle}?mode=json";
        using var response = await AtsHttp.GetWithRetryAsync(_http, url, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Lever returned {Status} for handle {Handle}", (int)response.StatusCode, handle);
            return Array.Empty<DiscoveredJobDto>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<DiscoveredJobDto>();

        var results = new List<DiscoveredJobDto>(doc.RootElement.GetArrayLength());
        foreach (var job in doc.RootElement.EnumerateArray())
        {
            var externalId = GetString(job, "id");
            if (string.IsNullOrEmpty(externalId)) continue;

            var title = GetString(job, "text") ?? "(sem título)";
            var absoluteUrl = GetString(job, "hostedUrl") ?? GetString(job, "applyUrl") ?? $"https://jobs.lever.co/{handle}/{externalId}";

            string? location = null, department = null;
            if (job.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Object)
            {
                location = GetString(cats, "location");
                department = GetString(cats, "team");
            }

            var html = GetString(job, "description");
            var plain = GetString(job, "descriptionPlain") ?? AtsHttp.HtmlToText(html);
            var published = GetEpochMs(job, "createdAt");

            results.Add(new DiscoveredJobDto(
                externalId, title, company.Name, location, department,
                html, plain, absoluteUrl, Provider,
                PublishedAtUtc: published, UpdatedAtUtc: published, Language: null));
        }

        return results;
    }

    internal static string? ExtractHandle(Company company)
    {
        foreach (var candidate in new[] { company.CareersUrl, company.WebsiteUrl })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var m = HandleRegex().Match(candidate);
            if (m.Success) return m.Groups["handle"].Value;
        }
        return null;
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? GetEpochMs(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        return null;
    }

    [GeneratedRegex(@"(?:jobs|api)\.lever\.co/(?:v0/postings/)?(?<handle>[A-Za-z0-9_.-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HandleRegex();
}
