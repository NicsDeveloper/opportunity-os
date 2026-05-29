using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Persistence port used by <see cref="JobDiscoveryService"/>. Implemented in
/// Infrastructure over EF Core so the Application layer stays storage-agnostic.
/// </summary>
public interface IDiscoveryStore
{
    /// <summary>Companies ordered with strategic/high priority first.</summary>
    Task<IReadOnlyList<Company>> GetCompaniesByPriorityAsync(CancellationToken ct);

    Task<Company?> GetCompanyAsync(Guid companyId, CancellationToken ct);

    /// <summary>Existing posting matched by (SourceProvider, ExternalId), or null.</summary>
    Task<JobPosting?> FindJobAsync(string sourceProvider, string externalId, CancellationToken ct);

    Task AddJobAsync(JobPosting job, CancellationToken ct);

    Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
