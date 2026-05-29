using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.AI;

namespace OpportunityOS.Infrastructure.Ai;

public sealed class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 2000;
}

/// <summary>
/// Anthropic (Claude) Messages API provider. Active only when an Anthropic API
/// key is configured. JSON output is requested via the prompt (the API has no
/// json_object mode); <see cref="AiJson"/> tolerantly extracts the JSON object.
/// </summary>
public sealed class AnthropicLlmProvider : ILlmProvider
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicLlmProvider> _logger;

    public AnthropicLlmProvider(HttpClient http, AnthropicOptions options, ILogger<AnthropicLlmProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public string ModelName => _options.Model;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        if (!IsConfigured)
            return LlmResponse.Fail(ModelName, request.PromptVersion, "Anthropic API key not configured");

        var system = request.JsonMode
            ? request.SystemPrompt + " Responda SOMENTE com um único objeto JSON válido, sem markdown."
            : request.SystemPrompt;

        var payload = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            temperature = 0.3,
            system,
            messages = new[] { new { role = "user", content = request.UserPrompt } }
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        httpReq.Headers.Add("x-api-key", _options.ApiKey);
        httpReq.Headers.Add("anthropic-version", ApiVersion);

        try
        {
            using var response = await _http.SendAsync(httpReq, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Anthropic returned {Status}", (int)response.StatusCode);
                return LlmResponse.Fail(ModelName, request.PromptVersion, $"HTTP {(int)response.StatusCode}: {Truncate(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();

            return string.IsNullOrWhiteSpace(text)
                ? LlmResponse.Fail(ModelName, request.PromptVersion, "Empty content")
                : LlmResponse.Ok(text, ModelName, request.PromptVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic call failed");
            return LlmResponse.Fail(ModelName, request.PromptVersion, ex.Message);
        }
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}
