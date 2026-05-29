using System.Net;
using System.Text.RegularExpressions;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Small HTTP helpers shared by ATS providers: a bounded retry with backoff for
/// transient failures, and lightweight HTML-to-text conversion for descriptions.
/// </summary>
public static partial class AtsHttp
{
    public static async Task<HttpResponseMessage> GetWithRetryAsync(
        HttpClient client, string url, CancellationToken ct, int maxAttempts = 3)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await client.GetAsync(url, ct);
                if (IsTransient(response.StatusCode) && attempt < maxAttempts)
                {
                    await BackoffAsync(attempt, ct);
                    continue;
                }
                return response;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                last = ex;
                await BackoffAsync(attempt, ct);
            }
        }
        throw last ?? new HttpRequestException($"Failed to GET {url} after {maxAttempts} attempts.");
    }

    private static bool IsTransient(HttpStatusCode code) =>
        code == HttpStatusCode.TooManyRequests || (int)code >= 500;

    private static Task BackoffAsync(int attempt, CancellationToken ct) =>
        Task.Delay(TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1)), ct);

    /// <summary>Decode entities and strip tags to a readable plain-text description.</summary>
    public static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var decoded = WebUtility.HtmlDecode(html);
        var noTags = TagRegex().Replace(decoded, " ");
        return WhitespaceRegex().Replace(noTags, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex("\\s{2,}")]
    private static partial Regex WhitespaceRegex();
}
