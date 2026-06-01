using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// Audit record for a single query executed against a provider during a campaign run:
/// how many results came back, how many were new vs duplicate.
/// </summary>
public sealed class SearchQueryExecution
{
    public Guid Id { get; private set; }
    public Guid SearchCampaignId { get; private set; }
    public string Query { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public SearchQueryExecutionStatus Status { get; private set; }
    public int ResultsCount { get; private set; }
    public int NewCandidatesCount { get; private set; }
    public int DuplicateCandidatesCount { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public string? ErrorMessage { get; private set; }

    private SearchQueryExecution() { }

    public static SearchQueryExecution Start(Guid campaignId, string query, string provider) => new()
    {
        Id = Guid.NewGuid(),
        SearchCampaignId = campaignId,
        Query = query,
        Provider = provider,
        Status = SearchQueryExecutionStatus.Running,
        StartedAtUtc = DateTime.UtcNow
    };

    public void Succeed(int results, int newCandidates, int duplicates)
    {
        ResultsCount = results;
        NewCandidatesCount = newCandidates;
        DuplicateCandidatesCount = duplicates;
        Status = SearchQueryExecutionStatus.Succeeded;
        FinishedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string error)
    {
        Status = SearchQueryExecutionStatus.Failed;
        ErrorMessage = error.Length <= 2000 ? error : error[..2000];
        FinishedAtUtc = DateTime.UtcNow;
    }
}
