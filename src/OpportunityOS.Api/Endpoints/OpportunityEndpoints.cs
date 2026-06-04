using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>
/// The opportunity pipeline board, SCOPED TO THE CALLER'S WORKSPACE. Every read/write is limited to
/// opportunities whose candidate profile belongs to the current workspace (spec §19).
/// </summary>
public static class OpportunityEndpoints
{
    public static void MapOpportunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/opportunities").WithTags("Opportunities").RequireAuthorization();

        // Optionally scope to one profile (must be owned); otherwise all of this workspace's profiles.
        group.MapGet("/", async (
            Guid? candidateProfileId, ICurrentUserContext user, OpportunityOsDbContext db,
            ICurrentCandidateProfileProvider profiles, CancellationToken ct) =>
        {
            var profileIds = await ResolveScopeAsync(candidateProfileId, user, profiles, db, ct);
            var items = await db.Opportunities
                .Where(o => profileIds.Contains(o.CandidateProfileId))
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync(ct);
            return Results.Ok(items.Select(o => o.ToResponse()));
        });

        // Pending follow-ups (due now or earlier), scoped to the workspace. Before {id} to avoid clash.
        group.MapGet("/follow-ups", async (
            ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profileIds = await WorkspaceProfileIdsAsync(user, db, ct);
            var now = DateTime.UtcNow;
            var due = await db.Opportunities
                .Where(o => profileIds.Contains(o.CandidateProfileId)
                    && o.NextFollowUpAtUtc != null && o.NextFollowUpAtUtc <= now)
                .OrderBy(o => o.NextFollowUpAtUtc)
                .ToListAsync(ct);
            return Results.Ok(due.Select(o => o.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var o = await OwnedOpportunity(id, user, db, ct);
            return o is null ? Results.NotFound() : Results.Ok(o.ToResponse());
        });

        // Manual status change — the only path allowed to set human-gated statuses.
        group.MapPut("/{id:guid}/status", async (
            Guid id, OpportunityStatusRequest req, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (!Enum.TryParse<OpportunityStatus>(req.Status, ignoreCase: true, out var status))
                return Results.BadRequest($"Invalid status '{req.Status}'.");

            var o = await OwnedOpportunity(id, user, db, ct);
            if (o is null) return Results.NotFound();
            o.SetStatusManually(status);
            await db.SaveChangesAsync(ct);
            return Results.Ok(o.ToResponse());
        });

        group.MapPut("/{id:guid}/notes", async (
            Guid id, OpportunityNotesRequest req, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var o = await OwnedOpportunity(id, user, db, ct);
            if (o is null) return Results.NotFound();
            o.SetNotes(req.Notes);
            await db.SaveChangesAsync(ct);
            return Results.Ok(o.ToResponse());
        });

        group.MapPut("/{id:guid}/follow-up", async (
            Guid id, OpportunityFollowUpRequest req, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var o = await OwnedOpportunity(id, user, db, ct);
            if (o is null) return Results.NotFound();
            o.SetFollowUp(req.NextFollowUpAtUtc);
            await db.SaveChangesAsync(ct);
            return Results.Ok(o.ToResponse());
        });
    }

    private static async Task<List<Guid>> WorkspaceProfileIdsAsync(
        ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct)
    {
        var ws = await user.GetWorkspaceIdAsync(ct);
        return await db.CandidateProfiles.Where(p => p.WorkspaceId == ws).Select(p => p.Id).ToListAsync(ct);
    }

    // candidateProfileId given → just that (owned, else 403 via provider); otherwise all workspace profiles.
    private static async Task<List<Guid>> ResolveScopeAsync(
        Guid? candidateProfileId, ICurrentUserContext user, ICurrentCandidateProfileProvider profiles,
        OpportunityOsDbContext db, CancellationToken ct)
    {
        if (candidateProfileId is not null)
        {
            var resolved = await profiles.ResolveIdAsync(candidateProfileId, ct);
            return resolved is { } r ? new List<Guid> { r } : new();
        }
        return await WorkspaceProfileIdsAsync(user, db, ct);
    }

    private static async Task<Domain.Entities.Opportunity?> OwnedOpportunity(
        Guid id, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct)
    {
        var profileIds = await WorkspaceProfileIdsAsync(user, db, ct);
        return await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id && profileIds.Contains(o.CandidateProfileId), ct);
    }
}
