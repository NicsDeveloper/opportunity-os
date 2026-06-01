using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

/// <summary>Persistence port for the Firehose (campaigns, query executions, raw candidates).</summary>
public interface IFirehoseStore
{
    Task AddCampaignAsync(SearchCampaign campaign, CancellationToken ct);
    Task<SearchCampaign?> GetCampaignAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SearchCampaign>> GetCampaignsAsync(CancellationToken ct);

    /// <summary>True if a raw candidate with this discovered URL already exists (URL dedup).</summary>
    Task<bool> RawCandidateExistsByUrlAsync(string url, CancellationToken ct);
    /// <summary>True if a raw candidate with this semantic fingerprint already exists.</summary>
    Task<bool> RawCandidateExistsByFingerprintAsync(string fingerprint, CancellationToken ct);
    Task AddRawCandidateAsync(RawJobCandidate candidate, CancellationToken ct);

    Task AddQueryExecutionAsync(SearchQueryExecution execution, CancellationToken ct);
    Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct);

    Task<IReadOnlyList<RawJobCandidate>> GetRawCandidatesAsync(int take, CancellationToken ct);
    Task<int> CountRawCandidatesAsync(DateTime? sinceUtc, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
