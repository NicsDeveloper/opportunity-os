namespace OpportunityOS.Application.Discovery;

/// <summary>One organic search result, raw (no " vaga" heuristics, no filtering).</summary>
public sealed record RawSearchResult(string Title, string Url, string? Snippet, DateTime? PublishedAtUtc = null);

/// <summary>
/// A web search provider used by the Firehose: runs a single query verbatim and returns
/// raw results. Decoupled from the qualified-flow IJobSearchProvider on purpose — the
/// Firehose wants breadth and per-query control, not the curated pipeline.
/// </summary>
public interface IRawSearchProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    Task<IReadOnlyList<RawSearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct);
}
