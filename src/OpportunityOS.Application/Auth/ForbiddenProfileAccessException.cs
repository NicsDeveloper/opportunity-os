namespace OpportunityOS.Application.Auth;

/// <summary>
/// Thrown when a request supplies an explicit candidate-profile id that does not belong to the
/// caller's workspace. The API maps this to HTTP 403 so cross-tenant access is rejected (not
/// silently downgraded to the caller's own default profile).
/// </summary>
public sealed class ForbiddenProfileAccessException : Exception
{
    public ForbiddenProfileAccessException(Guid candidateProfileId)
        : base($"Candidate profile {candidateProfileId} does not belong to the current workspace.")
        => CandidateProfileId = candidateProfileId;

    public Guid CandidateProfileId { get; }
}
