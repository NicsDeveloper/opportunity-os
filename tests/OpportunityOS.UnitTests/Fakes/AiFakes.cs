using OpportunityOS.Application.AI;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.UnitTests.Fakes;

/// <summary>LLM provider stub with controllable output and configured state.</summary>
public sealed class StubLlmProvider : ILlmProvider
{
    private readonly Func<LlmRequest, LlmResponse> _responder;

    public StubLlmProvider(Func<LlmRequest, LlmResponse> responder, bool isConfigured = true)
    {
        _responder = responder;
        IsConfigured = isConfigured;
    }

    public string ModelName => "stub";
    public bool IsConfigured { get; }

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct) =>
        Task.FromResult(_responder(request));

    public static StubLlmProvider ReturningRaw(string raw) =>
        new(req => LlmResponse.Ok(raw, "stub", req.PromptVersion));

    public static StubLlmProvider Throwing() =>
        new(_ => throw new InvalidOperationException("llm boom"));

    public static StubLlmProvider NotConfigured() =>
        new(req => LlmResponse.Ok("{}", "stub", req.PromptVersion), isConfigured: false);
}

/// <summary>Captures prompt execution logs for assertions.</summary>
public sealed class CapturingLogStore : IPromptExecutionLogStore
{
    public List<PromptExecutionLog> Logs { get; } = new();

    public Task SaveAsync(PromptExecutionLog log, CancellationToken ct)
    {
        Logs.Add(log);
        return Task.CompletedTask;
    }
}
