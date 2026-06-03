using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Profiles;

/// <summary>
/// Central resolution of "which candidate profile this request is for".
/// Phase 1 (no auth): an explicit <paramref name="candidateProfileId"/> wins, otherwise
/// the default profile, otherwise the most recent one. A later auth phase can resolve by
/// the authenticated user without touching the call sites that depend on this port.
/// </summary>
public interface ICurrentCandidateProfileProvider
{
    /// <summary>Resolve the profile for an optional explicit id, or the default/most-recent.</summary>
    Task<CandidateProfile?> GetAsync(Guid? candidateProfileId, CancellationToken cancellationToken);

    /// <summary>Resolve the profile's id only (cheap path), or null when none exists.</summary>
    Task<Guid?> ResolveIdAsync(Guid? candidateProfileId, CancellationToken cancellationToken);
}
