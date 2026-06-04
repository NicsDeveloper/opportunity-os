namespace OpportunityOS.Application.Auth;

/// <summary>
/// Ambient access to the authenticated caller for the current request. Implemented over the HTTP
/// context in the API; faked in tests. The Auth Workspace MVP resolves the caller's single workspace
/// from their user id — this is the anchor every per-profile query is scoped to.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>The authenticated user's id, or null when the request is anonymous.</summary>
    Guid? UserId { get; }

    /// <summary>True when a user is authenticated on this request.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The caller's workspace id (one per user in the MVP), or null when anonymous / not yet provisioned.</summary>
    Task<Guid?> GetWorkspaceIdAsync(CancellationToken cancellationToken);
}
