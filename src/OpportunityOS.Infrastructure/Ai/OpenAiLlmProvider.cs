using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.AI;

namespace OpportunityOS.Infrastructure.Ai;

public sealed class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4.1-mini";
    /// <summary>OpenAI-compatible Chat Completions endpoint. Override for Groq/Ollama/Together/DeepSeek.</summary>
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    /// <summary>Friendly tag shown in audit logs/UI (e.g. "OpenAI", "Groq").</summary>
    public string ProviderTag { get; set; } = "OpenAI";
}

/// <summary>
/// OpenAI-compatible Chat Completions provider. Drives OpenAI itself by default, but also any
/// service that mirrors the same API (Groq, Ollama, Together.ai, DeepSeek, ...) — just point
/// <see cref="OpenAiOptions.Endpoint"/> at it. Only active when an API key is present (otherwise
/// <see cref="FakeLlmProvider"/> is registered instead).
/// </summary>
public sealed class OpenAiLlmProvider : ILlmProvider
{

    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiLlmProvider> _logger;

    public OpenAiLlmProvider(HttpClient http, OpenAiOptions options, ILogger<OpenAiLlmProvider> logger)
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
            return LlmResponse.Fail(ModelName, request.PromptVersion, $"{_options.ProviderTag} API key not configured");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            ["temperature"] = 0.3
        };
        if (request.JsonMode)
            payload["response_format"] = new { type = "json_object" };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        httpReq.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

        try
        {
            using var response = await _http.SendAsync(httpReq, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("{Provider} returned {Status}", _options.ProviderTag, (int)response.StatusCode);
                return LlmResponse.Fail(ModelName, request.PromptVersion, $"HTTP {(int)response.StatusCode}: {Truncate(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content)
                ? LlmResponse.Fail(ModelName, request.PromptVersion, "Empty content")
                : LlmResponse.Ok(content, ModelName, request.PromptVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Provider} call failed", _options.ProviderTag);
            return LlmResponse.Fail(ModelName, request.PromptVersion, ex.Message);
        }
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}
