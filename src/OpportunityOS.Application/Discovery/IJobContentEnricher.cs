namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Fetches the real text of a job posting page so thin search snippets don't cause
/// under-scoring. Implemented in Infrastructure (HTTP + tag strip, Playwright fallback).
/// </summary>
public interface IJobContentEnricher
{
    /// <summary>Visible page text (truncated), or null if it couldn't be fetched.</summary>
    Task<string?> FetchTextAsync(string url, CancellationToken ct);
}
