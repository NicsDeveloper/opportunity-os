namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Guards daily spend (search queries / LLM analyses) so massive discovery never blows the
/// cost or rate limits. In-memory, resets daily (UTC). Never throws — callers check first.
/// </summary>
public interface IQueryBudgetManager
{
    /// <summary>True if <paramref name="provider"/> still has daily budget left.</summary>
    Task<bool> CanExecuteAsync(string provider, CancellationToken ct);

    /// <summary>Record consumed budget for a provider (e.g. number of search requests).</summary>
    Task RecordExecutionAsync(string provider, int cost, CancellationToken ct);

    /// <summary>Consumed/limit snapshot for a provider (for metrics + UI).</summary>
    BudgetSnapshot Snapshot(string provider);
}

public sealed record BudgetSnapshot(string Provider, int Consumed, int Limit)
{
    public int Remaining => Math.Max(0, Limit - Consumed);
    public bool Exhausted => Consumed >= Limit;
}

/// <summary>Daily budgets, bound from configuration section "DiscoveryBudget".</summary>
public sealed class DiscoveryBudgetOptions
{
    public int SerperDailyQueries { get; set; } = 1000;
    public int SerperMaxResultsPerQuery { get; set; } = 20;
    public int AggressiveSearchMaxQueries { get; set; } = 250;
    public int QuickSearchMaxQueries { get; set; } = 20;
    public int LlmDailyAutoAnalyses { get; set; } = 50;
    public int LlmManualAnalyses { get; set; } = 200;
}
