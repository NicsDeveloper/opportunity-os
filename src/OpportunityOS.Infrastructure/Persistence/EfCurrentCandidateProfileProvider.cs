using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

/// <summary>
/// EF resolution of the current candidate profile: explicit id -> IsDefault -> most recent.
/// No global mutable "active" state — the selected profile is supplied per request.
/// </summary>
public sealed class EfCurrentCandidateProfileProvider : ICurrentCandidateProfileProvider
{
    private readonly OpportunityOsDbContext _db;

    public EfCurrentCandidateProfileProvider(OpportunityOsDbContext db) => _db = db;

    public async Task<CandidateProfile?> GetAsync(Guid? candidateProfileId, CancellationToken ct)
    {
        if (candidateProfileId is { } id)
        {
            var explicitProfile = await _db.CandidateProfiles.FindAsync([id], ct);
            if (explicitProfile is not null) return explicitProfile;
        }

        return await _db.CandidateProfiles
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Guid?> ResolveIdAsync(Guid? candidateProfileId, CancellationToken ct)
    {
        if (candidateProfileId is { } id &&
            await _db.CandidateProfiles.AnyAsync(p => p.Id == id, ct))
            return id;

        var resolved = await _db.CandidateProfiles
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        return resolved;
    }
}
