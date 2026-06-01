using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// SmartRecruiters public Posting API:
/// GET https://api.smartrecruiters.com/v1/companies/{identifier}/postings  (list)
/// GET https://api.smartrecruiters.com/v1/companies/{identifier}/postings/{id} (detail)
/// Both public, no auth. Identifier comes from jobs.smartrecruiters.com/{identifier}.
/// </summary>
public sealed partial class SmartRecruitersJobSourceProvider : IJobSourceProvider
{
    public const string Provider = "SmartRecruiters";
    private const string ApiBase = "https://api.smartrecruiters.com/v1/companies";
    private const int MaxPostings = 40; // bound the per-posting detail calls

    private readonly HttpClient _http;
    private readonly ILogger<SmartRecruitersJobSourceProvider> _logger;

    public SmartRecruitersJobSourceProvider(HttpClient http, ILogger<SmartRecruitersJobSourceProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public string ProviderName => Provider;

    public bool CanHandle(Company company) => ExtractIdentifier(company) is not null;

    public async Task<IReadOnlyCollection<DiscoveredJobDto>> DiscoverJobsAsync(Company company, CancellationToken ct)
    {
        var identifier = ExtractIdentifier(company);
        if (identifier is null) return Array.Empty<DiscoveredJobDto>();

        var listUrl = $"{ApiBase}/{identifier}/postings?limit=100&offset=0";
        using var listResp = await AtsHttp.GetWithRetryAsync(_http, listUrl, ct);
        if (!listResp.IsSuccessStatusCode)
        {
            _logger.LogWarning("SmartRecruiters returned {Status} for {Id}", (int)listResp.StatusCode, identifier);
            return Array.Empty<DiscoveredJobDto>();
        }

        await using var listStream = await listResp.Content.ReadAsStreamAsync(ct);
        using var listDoc = await JsonDocument.ParseAsync(listStream, cancellationToken: ct);
        if (!listDoc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return Array.Empty<DiscoveredJobDto>();

        var results = new List<DiscoveredJobDto>();
        var processed = 0;
        foreach (var posting in content.EnumerateArray())
        {
            if (processed++ >= MaxPostings) break;

            var externalId = GetString(posting, "id");
            if (string.IsNullOrEmpty(externalId)) continue;

            var title = GetString(posting, "name") ?? "(sem título)";
            var location = posting.TryGetProperty("location", out var loc) ? BuildLocation(loc) : null;
            var department = posting.TryGetProperty("department", out var dep) ? GetString(dep, "label") : null;
            var published = GetDate(posting, "releasedDate");

            var (description, postingUrl) = await FetchDetailAsync(identifier, externalId, ct);
            var absoluteUrl = postingUrl ?? $"https://jobs.smartrecruiters.com/{identifier}/{externalId}";

            results.Add(new DiscoveredJobDto(
                externalId, title, company.Name, location, department,
                DescriptionHtml: null, DescriptionText: description, AbsoluteUrl: absoluteUrl,
                SourceProvider: Provider, PublishedAtUtc: published, UpdatedAtUtc: published, Language: null));
        }
        return results;
    }

    private async Task<(string Description, string? PostingUrl)> FetchDetailAsync(
        string identifier, string postingId, CancellationToken ct)
    {
        try
        {
            var url = $"{ApiBase}/{identifier}/postings/{postingId}";
            using var resp = await AtsHttp.GetWithRetryAsync(_http, url, ct);
            if (!resp.IsSuccessStatusCode) return (string.Empty, null);

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var postingUrl = GetString(root, "postingUrl") ?? GetString(root, "applyUrl");

            var sb = new StringBuilder();
            if (root.TryGetProperty("jobAd", out var jobAd) &&
                jobAd.TryGetProperty("sections", out var sections))
            {
                foreach (var key in new[] { "companyDescription", "jobDescription", "qualifications", "additionalInformation" })
                    if (sections.TryGetProperty(key, out var sec) && GetString(sec, "text") is { Length: > 0 } text)
                        sb.Append(AtsHttp.HtmlToText(text)).Append('\n');
            }
            return (sb.ToString().Trim(), postingUrl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SmartRecruiters detail fetch failed for {Id}", postingId);
            return (string.Empty, null);
        }
    }

    internal static string? ExtractIdentifier(Company company)
    {
        foreach (var candidate in new[] { company.CareersUrl, company.WebsiteUrl })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var m = IdRegex().Match(candidate);
            if (m.Success) return m.Groups["id"].Value;
        }
        return null;
    }

    private static string BuildLocation(JsonElement loc)
    {
        var full = GetString(loc, "fullLocation");
        var remote = loc.TryGetProperty("remote", out var r) && r.ValueKind == JsonValueKind.True;
        if (!string.IsNullOrWhiteSpace(full)) return remote ? $"{full} (remote)" : full!;
        return remote ? "Remote" : GetString(loc, "city") ?? "";
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? GetDate(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var dt)
            ? dt.ToUniversalTime() : null;

    [GeneratedRegex(@"(?:jobs|careers)\.smartrecruiters\.com/(?<id>[A-Za-z0-9._-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex IdRegex();
}
