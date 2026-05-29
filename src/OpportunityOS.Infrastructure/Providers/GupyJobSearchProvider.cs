using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Gupy public portal API (where most Brazilian fintechs post). Keyword-based,
/// cross-company: GET https://portal.api.gupy.io/api/v1/jobs?jobName={kw}.
/// Public, no auth. One request per keyword; results are de-duplicated by job id.
/// </summary>
public sealed class GupyJobSearchProvider : IJobSearchProvider
{
    public const string Provider = "Gupy";
    private const string ApiBase = "https://portal.api.gupy.io/api/v1/jobs";
    private const int PerKeyword = 50;

    private readonly HttpClient _http;
    private readonly ILogger<GupyJobSearchProvider> _logger;

    public GupyJobSearchProvider(HttpClient http, ILogger<GupyJobSearchProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string ProviderName => Provider;

    public async Task<IReadOnlyCollection<DiscoveredJobDto>> SearchAsync(
        IReadOnlyCollection<string> keywords, CancellationToken ct)
    {
        var byId = new Dictionary<string, DiscoveredJobDto>(StringComparer.Ordinal);

        foreach (var keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword)) continue;
            var url = $"{ApiBase}?jobName={Uri.EscapeDataString(keyword)}&limit={PerKeyword}&offset=0";

            using var response = await AtsHttp.GetWithRetryAsync(_http, url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gupy returned {Status} for keyword '{Keyword}'", (int)response.StatusCode, keyword);
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var job in data.EnumerateArray())
            {
                var dto = Map(job);
                if (dto is not null) byId[dto.ExternalId] = dto;
            }
        }

        return byId.Values.ToList();
    }

    private static DiscoveredJobDto? Map(JsonElement job)
    {
        var externalId = job.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
            ? idEl.GetInt64().ToString()
            : null;
        if (externalId is null) return null;

        var title = GetString(job, "name") ?? "(sem título)";
        var company = GetString(job, "careerPageName") ?? "(empresa)";
        var url = GetString(job, "jobUrl") ?? GetString(job, "careerPageUrl") ?? "";
        var description = GetString(job, "description") ?? string.Empty;
        var published = GetDate(job, "publishedDate");
        var location = BuildLocation(job);

        return new DiscoveredJobDto(
            externalId, title, company, location, Department: null,
            DescriptionHtml: null, DescriptionText: description, AbsoluteUrl: url,
            SourceProvider: Provider, PublishedAtUtc: published, UpdatedAtUtc: published,
            Language: "pt-BR");
    }

    private static string? BuildLocation(JsonElement job)
    {
        var remote = job.TryGetProperty("isRemoteWork", out var r) && r.ValueKind == JsonValueKind.True;
        var workplace = GetString(job, "workplaceType");
        var city = GetString(job, "city");
        var state = GetString(job, "state");
        var country = GetString(job, "country") ?? "Brasil";

        if (remote || workplace == "remote") return $"Remoto - {country}";
        var place = string.Join("/", new[] { city, state }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(place) ? country : $"{place} ({workplace ?? "onsite"})";
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? GetDate(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var dt)
            ? dt.ToUniversalTime() : null;
}
