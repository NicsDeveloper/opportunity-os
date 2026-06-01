namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// Shared daily budget for Google Custom Search queries (free tier = 100/day).
/// Every CSE caller — onboarding, website backfill, web job search — consumes from
/// this one guard so the quota is divided rationally and never blown. Resets daily (UTC).
/// </summary>
public sealed class GoogleQuotaGuard
{
    private readonly int _daily;
    private readonly object _lock = new();
    private DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _used;

    public GoogleQuotaGuard(int dailyBudget) => _daily = Math.Max(0, dailyBudget);

    /// <summary>Reserve one query if budget remains. Returns false when exhausted.</summary>
    public bool TryConsume()
    {
        lock (_lock)
        {
            Roll();
            if (_used >= _daily) return false;
            _used++;
            return true;
        }
    }

    public int Remaining
    {
        get { lock (_lock) { Roll(); return Math.Max(0, _daily - _used); } }
    }

    private void Roll()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today != _day) { _day = today; _used = 0; }
    }
}
