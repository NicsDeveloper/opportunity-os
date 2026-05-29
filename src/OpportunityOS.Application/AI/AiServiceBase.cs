using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

/// <summary>
/// Shared LLM execution pipeline: call the provider, validate JSON, audit the
/// execution, and fall back to a deterministic heuristic on any failure.
/// A failed LLM call NEVER throws out of here — the flow always continues.
/// </summary>
public abstract class AiServiceBase
{
    protected readonly ILlmProvider Llm;
    private readonly IPromptExecutionLogStore _audit;
    private readonly ILogger _logger;

    protected AiServiceBase(ILlmProvider llm, IPromptExecutionLogStore audit, ILogger logger)
    {
        Llm = llm;
        _audit = audit;
        _logger = logger;
    }

    protected abstract string ServiceName { get; }

    /// <summary>
    /// Run <paramref name="request"/>, deserialize the response to <typeparamref name="T"/>,
    /// and return it. On not-configured / call failure / invalid JSON, log and return
    /// <paramref name="fallback"/>().
    /// </summary>
    protected async Task<T> ExecuteAsync<T>(
        LlmRequest request,
        Func<T> fallback,
        Guid? jobId,
        Guid? profileId,
        CancellationToken ct)
    {
        if (!Llm.IsConfigured)
        {
            await AuditAsync(request.PromptVersion, "(LLM provider not configured)", success: false,
                usedFallback: true, error: "LLM not configured; using heuristic fallback", jobId, profileId, ct);
            return fallback();
        }

        LlmResponse response;
        try
        {
            response = await Llm.CompleteAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LlmFailed {Service} {Version}", ServiceName, request.PromptVersion);
            await AuditAsync(request.PromptVersion, ex.Message, success: false, usedFallback: true,
                error: ex.Message, jobId, profileId, ct);
            return fallback();
        }

        if (!response.Success)
        {
            _logger.LogWarning("LlmFailed {Service} {Version}: {Error}", ServiceName, request.PromptVersion, response.Error);
            await AuditAsync(request.PromptVersion, response.RawText, success: false, usedFallback: true,
                error: response.Error, jobId, profileId, ct);
            return fallback();
        }

        if (AiJson.TryDeserialize<T>(response.RawText, out var value, out var error) && value is not null)
        {
            await AuditAsync(request.PromptVersion, response.RawText, success: true, usedFallback: false,
                error: null, jobId, profileId, ct);
            return value;
        }

        _logger.LogWarning("LlmInvalidJson {Service} {Version}: {Error}", ServiceName, request.PromptVersion, error);
        await AuditAsync(request.PromptVersion, response.RawText, success: false, usedFallback: true,
            error: $"Invalid JSON: {error}", jobId, profileId, ct);
        return fallback();
    }

    private async Task AuditAsync(
        string version, string raw, bool success, bool usedFallback, string? error,
        Guid? jobId, Guid? profileId, CancellationToken ct)
    {
        try
        {
            var log = new PromptExecutionLog(
                ServiceName, version, Llm.ModelName, success, usedFallback, error, raw, jobId, profileId);
            await _audit.SaveAsync(log, ct);
        }
        catch (Exception ex)
        {
            // Auditing must never break the main flow.
            _logger.LogError(ex, "Failed to persist PromptExecutionLog for {Service}", ServiceName);
        }
    }
}
