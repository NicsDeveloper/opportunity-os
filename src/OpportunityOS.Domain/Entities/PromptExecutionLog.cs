namespace OpportunityOS.Domain.Entities;

/// <summary>
/// Audit record for a single LLM call. Spec requires every AI response to be
/// stored with promptVersion, modelName, createdAtUtc and the raw response.
/// </summary>
public sealed class PromptExecutionLog
{
    public Guid Id { get; private set; }
    public string Service { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public bool Success { get; private set; }
    public bool UsedFallback { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string RawResponse { get; private set; } = string.Empty;
    public Guid? JobPostingId { get; private set; }
    public Guid? CandidateProfileId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PromptExecutionLog() { }

    public PromptExecutionLog(
        string service,
        string promptVersion,
        string modelName,
        bool success,
        bool usedFallback,
        string? errorMessage,
        string rawResponse,
        Guid? jobPostingId,
        Guid? candidateProfileId)
    {
        Id = Guid.NewGuid();
        Service = service;
        PromptVersion = promptVersion;
        ModelName = modelName;
        Success = success;
        UsedFallback = usedFallback;
        ErrorMessage = errorMessage;
        RawResponse = rawResponse.Length > 20000 ? rawResponse[..20000] : rawResponse;
        JobPostingId = jobPostingId;
        CandidateProfileId = candidateProfileId;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
