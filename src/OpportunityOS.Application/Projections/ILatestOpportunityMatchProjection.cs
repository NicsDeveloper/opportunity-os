using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Projections;

/// <summary>Outcome of a projection rebuild.</summary>
public sealed record LatestMatchBackfillResult(int ProcessedPairs, int Created, int Updated, int Skipped);

/// <summary>
/// Keeps the <see cref="LatestOpportunityMatch"/> projection in sync. Every newly created
/// <see cref="OpportunityMatch"/> must be funnelled through <see cref="UpsertAsync"/> so the
/// feed/digest can read the latest match per (job, profile) without scanning every match.
/// </summary>
public interface ILatestOpportunityMatchProjection
{
    /// <summary>
    /// Point the projection for the match's (job, profile) at <paramref name="match"/> when it
    /// is the newest. Stages the change on the unit of work but does NOT save — the caller saves
    /// (so the match and its projection commit together).
    /// </summary>
    Task UpsertAsync(OpportunityMatch match, CancellationToken cancellationToken);

    /// <summary>Rebuild the whole projection from existing matches. Saves its own changes.</summary>
    Task<LatestMatchBackfillResult> BackfillAsync(CancellationToken cancellationToken);
}
