using System.Collections.Concurrent;
using OpportunityOS.Application.Discovery;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// In-memory daily budget manager (resets at UTC midnight). Maps a provider/cost-center name
/// to its daily limit: anything containing "Serper" → SerperDailyQueries; "Llm" →
/// LlmDailyAutoAnalyses; otherwise unlimited. Thread-safe; never throws.
/// </summary>
public sealed class QueryBudgetManager : IQueryBudgetManager
{
    private readonly DiscoveryBudgetOptions _options;
    private readonly ConcurrentDictionary<string, int> _consumed = new(StringComparer.OrdinalIgnoreCase);
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly object _gate = new();

    public QueryBudgetManager(DiscoveryBudgetOptions options) => _options = options;

    public Task<bool> CanExecuteAsync(string provider, CancellationToken ct)
    {
        RollIfNewDay();
        var limit = LimitFor(provider);
        var consumed = _consumed.GetValueOrDefault(provider, 0);
        return Task.FromResult(consumed < limit);
    }

    public Task RecordExecutionAsync(string provider, int cost, CancellationToken ct)
    {
        RollIfNewDay();
        _consumed.AddOrUpdate(provider, Math.Max(0, cost), (_, c) => c + Math.Max(0, cost));
        return Task.CompletedTask;
    }

    public BudgetSnapshot Snapshot(string provider)
    {
        RollIfNewDay();
        return new BudgetSnapshot(provider, _consumed.GetValueOrDefault(provider, 0), LimitFor(provider));
    }

    private int LimitFor(string provider)
    {
        if (provider.Contains("Serper", StringComparison.OrdinalIgnoreCase)) return _options.SerperDailyQueries;
        if (provider.Contains("Llm", StringComparison.OrdinalIgnoreCase)) return _options.LlmDailyAutoAnalyses;
        return int.MaxValue;
    }

    private void RollIfNewDay()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today == _day) return;
        lock (_gate)
        {
            if (today == _day) return;
            _day = today;
            _consumed.Clear();
        }
    }
}
