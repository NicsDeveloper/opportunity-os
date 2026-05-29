using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// Audit record for a background/manual execution (discovery, analysis, digest...).
/// </summary>
public sealed class ExecutionRun
{
    public Guid Id { get; private set; }
    public string RunType { get; private set; } = string.Empty;
    public ExecutionRunStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public int ItemsProcessed { get; private set; }
    public int ItemsSucceeded { get; private set; }
    public int ItemsFailed { get; private set; }
    public string? ErrorMessage { get; private set; }

    private ExecutionRun() { }

    public static ExecutionRun Start(string runType) => new()
    {
        Id = Guid.NewGuid(),
        RunType = runType,
        Status = ExecutionRunStatus.Running,
        StartedAtUtc = DateTime.UtcNow
    };

    public void RecordSuccess(int count = 1)
    {
        ItemsProcessed += count;
        ItemsSucceeded += count;
    }

    public void RecordFailure(string? error = null, int count = 1)
    {
        ItemsProcessed += count;
        ItemsFailed += count;
        if (!string.IsNullOrWhiteSpace(error))
            ErrorMessage = Truncate(string.IsNullOrEmpty(ErrorMessage) ? error : $"{ErrorMessage} | {error}", 2000);
    }

    public void Complete()
    {
        FinishedAtUtc = DateTime.UtcNow;
        Status = ItemsFailed == 0
            ? ExecutionRunStatus.Succeeded
            : ItemsSucceeded > 0
                ? ExecutionRunStatus.PartiallyFailed
                : ExecutionRunStatus.Failed;
    }

    public void Fail(string error)
    {
        FinishedAtUtc = DateTime.UtcNow;
        Status = ExecutionRunStatus.Failed;
        ErrorMessage = Truncate(error, 2000);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
