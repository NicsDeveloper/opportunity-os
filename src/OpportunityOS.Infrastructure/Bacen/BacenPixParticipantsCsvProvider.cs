using System.Text;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Bacen;
using OpportunityOS.Infrastructure.Providers;

namespace OpportunityOS.Infrastructure.Bacen;

public sealed class BacenOptions
{
    /// <summary>Official Bacen Pix participants CSV (configurable snapshot URL).</summary>
    public string PixParticipantsCsvUrl { get; set; } = string.Empty;
}

/// <summary>
/// Downloads the official Bacen Pix participants CSV and parses it. The file is
/// Latin1-encoded and semicolon-separated; we decode accordingly. No HTML scraping,
/// no endpoint guessing — just the configured CSV snapshot.
/// </summary>
public sealed class BacenPixParticipantsCsvProvider : IBacenPixParticipantsCsvProvider
{
    private readonly HttpClient _http;
    private readonly BacenOptions _options;
    private readonly ILogger<BacenPixParticipantsCsvProvider> _logger;

    public BacenPixParticipantsCsvProvider(
        HttpClient http, BacenOptions options, ILogger<BacenPixParticipantsCsvProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<BacenPixParticipantDto>> GetParticipantsAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.PixParticipantsCsvUrl))
            throw new InvalidOperationException("Bacen:PixParticipantsCsvUrl is not configured.");

        using var response = await AtsHttp.GetWithRetryAsync(_http, _options.PixParticipantsCsvUrl, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Bacen CSV download failed: HTTP {(int)response.StatusCode}");

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var content = Encoding.Latin1.GetString(bytes); // ISO-8859-1, matches the file

        var rows = BacenCsvParser.Parse(content);
        _logger.LogInformation("Bacen CSV parsed: {Count} participant rows", rows.Count);
        return rows;
    }
}
