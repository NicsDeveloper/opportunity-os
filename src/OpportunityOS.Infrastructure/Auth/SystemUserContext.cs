using OpportunityOS.Application.Auth;

namespace OpportunityOS.Infrastructure.Auth;

/// <summary>
/// Default <see cref="ICurrentUserContext"/> for non-HTTP hosts (the Worker, design-time tooling):
/// there is no authenticated user, so no workspace. The API replaces this with an HTTP-backed
/// implementation. Background jobs (digest send-all, discovery) operate on profiles explicitly and
/// never rely on per-request workspace resolution.
/// </summary>
public sealed class SystemUserContext : ICurrentUserContext
{
    public Guid? UserId => null;
    public bool IsAuthenticated => false;
    public Task<Guid?> GetWorkspaceIdAsync(CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
}
