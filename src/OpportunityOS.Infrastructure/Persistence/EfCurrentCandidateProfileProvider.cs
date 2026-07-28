using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

/// <summary>
/// EF resolution of the current candidate profile, SCOPED TO THE CALLER'S WORKSPACE.
/// An explicit id wins only when it belongs to the caller's workspace — otherwise a
/// <see cref="ForbiddenProfileAccessException"/> is thrown (mapped to HTTP 403) so cross-tenant
/// access is rejected, never silently downgraded. With no explicit id we fall back to the
/// workspace's default (IsDefault) profile, then its most recent.
/// </summary>
public sealed class EfCurrentCandidateProfileProvider : ICurrentCandidateProfileProvider
{
    private readonly OpportunityOsDbContext _db;
    private readonly ICurrentUserContext _user;

    public EfCurrentCandidateProfileProvider(OpportunityOsDbContext db, ICurrentUserContext user)
    {
        _db = db;
        _user = user;
    }

    public async Task<CandidateProfile?> GetAsync(Guid? candidateProfileId, CancellationToken ct)
    {
        var workspaceId = await _user.GetWorkspaceIdAsync(ct);
        if (workspaceId is null)
        {
            // No workspace context: an explicit id can't be proven to belong to the caller.
            if (candidateProfileId is { } orphanId) throw new ForbiddenProfileAccessException(orphanId);
            return null;
        }

        if (candidateProfileId is { } id)
        {
            var explicitProfile = await _db.CandidateProfiles
                .FirstOrDefaultAsync(p => p.Id == id && p.WorkspaceId == workspaceId, ct);
            if (explicitProfile is null) throw new ForbiddenProfileAccessException(id);
            return explicitProfile;
        }

        return await DefaultForWorkspace(workspaceId.Value).FirstOrDefaultAsync(ct);
    }

    public async Task<Guid?> ResolveIdAsync(Guid? candidateProfileId, CancellationToken ct)
    {
        var workspaceId = await _user.GetWorkspaceIdAsync(ct);
        if (workspaceId is null)
        {
            if (candidateProfileId is { } orphanId) throw new ForbiddenProfileAccessException(orphanId);
            return null;
        }

        if (candidateProfileId is { } id)
        {
            var owned = await _db.CandidateProfiles
                .AnyAsync(p => p.Id == id && p.WorkspaceId == workspaceId, ct);
            if (!owned) throw new ForbiddenProfileAccessException(id);
            return id;
        }

        return await DefaultForWorkspace(workspaceId.Value)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
    }

    private IQueryable<CandidateProfile> DefaultForWorkspace(Guid workspaceId) =>
        _db.CandidateProfiles
            .Where(p => p.WorkspaceId == workspaceId)
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.CreatedAtUtc);
}
