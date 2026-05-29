namespace OpportunityOS.Application.AI;

/// <summary>A single LLM completion request.</summary>
public sealed record LlmRequest(
    string SystemPrompt,
    string UserPrompt,
    string PromptVersion,
    bool JsonMode = true);

/// <summary>The raw result of an LLM call plus the metadata required for auditing.</summary>
public sealed record LlmResponse(
    string RawText,
    string ModelName,
    string PromptVersion,
    bool Success,
    string? Error)
{
    public static LlmResponse Ok(string text, string model, string version) =>
        new(text, model, version, true, null);

    public static LlmResponse Fail(string model, string version, string error) =>
        new(string.Empty, model, version, false, error);
}

/// <summary>
/// Generic LLM gateway. Implementations: <c>OpenAiLlmProvider</c> (real) and
/// <c>FakeLlmProvider</c> (deterministic, used when no API key is configured).
/// </summary>
public interface ILlmProvider
{
    string ModelName { get; }

    /// <summary>False when the provider has no credentials and cannot make real calls.</summary>
    bool IsConfigured { get; }

    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}
