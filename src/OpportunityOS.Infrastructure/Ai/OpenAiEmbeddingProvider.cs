using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.AI;

namespace OpportunityOS.Infrastructure.Ai;

public sealed class OpenAiEmbeddingOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimensions { get; set; } = 1536;
}

/// <summary>Real semantic embeddings via OpenAI. Returns null on any failure (caller falls back).</summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private const string Endpoint = "https://api.openai.com/v1/embeddings";
    private readonly HttpClient _http;
    private readonly OpenAiEmbeddingOptions _options;
    private readonly ILogger<OpenAiEmbeddingProvider> _logger;

    public OpenAiEmbeddingProvider(HttpClient http, OpenAiEmbeddingOptions options, ILogger<OpenAiEmbeddingProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public string ModelName => _options.Model;
    public int Dimensions => _options.Dimensions;
    public bool IsRealModel => true;

    public async Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new { model = _options.Model, input = text })
            };
            req.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
            using var res = await _http.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI embeddings failed: {Status}", res.StatusCode);
                return null;
            }
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cancellationToken));
            var arr = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
            var vec = new float[arr.GetArrayLength()];
            for (var i = 0; i < vec.Length; i++) vec[i] = arr[i].GetSingle();
            // L2-normalize (defensive; OpenAI vectors are already unit-length).
            var norm = Math.Sqrt(vec.Sum(v => (double)v * v));
            if (norm > 0) for (var i = 0; i < vec.Length; i++) vec[i] = (float)(vec[i] / norm);
            return vec;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI embeddings call threw");
            return null;
        }
    }
}
