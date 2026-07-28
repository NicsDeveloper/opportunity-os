using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// Denormalized projection: the CURRENT (latest) <see cref="OpportunityMatch"/> for a
/// (job, profile) pair. <see cref="OpportunityMatch"/> is immutable and append-only, so the
/// feed/digest need "the latest match per (JobPostingId, CandidateProfileId)". Computing that
/// in memory over every match does not scale once there are many profiles × score versions —
/// this table lets queries seek the latest row directly. It is a derived cache: rebuildable
/// at any time from <see cref="OpportunityMatch"/> via the backfill.
/// </summary>
public sealed class LatestOpportunityMatch
{
    public Guid Id { get; private set; }

    public Guid JobPostingId { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public Guid OpportunityMatchId { get; private set; }

    public int OverallScore { get; private set; }
    public MatchRecommendation Recommendation { get; private set; }
    public string EngineVersion { get; private set; } = string.Empty;

    /// <summary>CreatedAtUtc of the underlying match — the freshness used to decide upserts.</summary>
    public DateTime MatchCreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private LatestOpportunityMatch() { }

    public static LatestOpportunityMatch From(OpportunityMatch match)
    {
        var projection = new LatestOpportunityMatch
        {
            Id = Guid.NewGuid(),
            JobPostingId = match.JobPostingId,
            CandidateProfileId = match.CandidateProfileId,
        };
        projection.UpdateFrom(match);
        return projection;
    }

    /// <summary>
    /// Point this projection at <paramref name="match"/> (same job+profile expected).
    /// Idempotent and safe to call with an older match: it only moves forward in time.
    /// Returns true when the projection actually changed.
    /// </summary>
    public bool UpdateFrom(OpportunityMatch match)
    {
        if (match.JobPostingId != JobPostingId || match.CandidateProfileId != CandidateProfileId)
            throw new InvalidOperationException(
                "LatestOpportunityMatch can only be updated from a match of the same (job, profile).");

        // Already pointing at this exact match — nothing to do (keeps re-backfill idempotent).
        if (OpportunityMatchId == match.Id) return false;

        // Never replace a newer match with an older one (rescore/LLM may arrive out of order).
        if (OpportunityMatchId != Guid.Empty && match.CreatedAtUtc < MatchCreatedAtUtc)
            return false;

        OpportunityMatchId = match.Id;
        OverallScore = match.OverallScore;
        Recommendation = match.Recommendation;
        EngineVersion = match.EngineVersion;
        MatchCreatedAtUtc = match.CreatedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }
}
