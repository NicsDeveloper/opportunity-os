using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>
/// Candidate profiles, SCOPED TO THE CALLER'S WORKSPACE. Every read/write filters by the current
/// workspace, so a user only ever sees and mutates their own profiles (spec §19.4).
/// </summary>
public static class CandidateProfileEndpoints
{
    public static void MapCandidateProfileEndpoints(this IEndpointRouteBuilder app)
    {
        MapSingular(app);
        MapPlural(app);
    }

    // Back-compat: the original singular route returns the CURRENT (default/most-recent) profile
    // for this workspace. Resolution already goes through the workspace-scoped provider.
    private static void MapSingular(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/candidate-profile").WithTags("CandidateProfile").RequireAuthorization();

        group.MapGet("/", async (ICurrentCandidateProfileProvider provider, CancellationToken ct) =>
        {
            var profile = await provider.GetAsync(null, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToResponse());
        });

        group.MapGet("/{id:guid}", async (Guid id, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.Id == id && p.WorkspaceId == ws, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToResponse());
        });

        group.MapPost("/", async (CandidateProfileRequest req, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            if (ws is null) return Results.BadRequest(new { error = "No workspace for this user." });
            var profile = FromRequest(req);
            profile.AssignWorkspace(ws.Value);
            if (!await db.CandidateProfiles.AnyAsync(p => p.WorkspaceId == ws, ct)) profile.SetDefault(true);
            db.CandidateProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/candidate-profiles/{profile.Id}", profile.ToResponse());
        });

        group.MapPut("/{id:guid}", async (Guid id, CandidateProfileRequest req, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.Id == id && p.WorkspaceId == ws, ct);
            if (profile is null) return Results.NotFound();
            ApplyUpdate(profile, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(profile.ToResponse());
        });
    }

    // Multi-profile collection API.
    private static void MapPlural(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/candidate-profiles").WithTags("CandidateProfile").RequireAuthorization();

        group.MapGet("/", async (ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            var profiles = await db.CandidateProfiles
                .Where(p => p.WorkspaceId == ws)
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.DisplayName)
                .ToListAsync(ct);
            return Results.Ok(profiles.Select(p => p.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.Id == id && p.WorkspaceId == ws, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToResponse());
        });

        group.MapPost("/", async (CandidateProfileRequest req, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            if (ws is null) return Results.BadRequest(new { error = "No workspace for this user." });
            var profile = FromRequest(req);
            profile.AssignWorkspace(ws.Value);
            // The first profile created in a workspace becomes its default anchor automatically.
            if (!await db.CandidateProfiles.AnyAsync(p => p.WorkspaceId == ws, ct)) profile.SetDefault(true);
            db.CandidateProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/candidate-profiles/{profile.Id}", profile.ToResponse());
        });

        group.MapPut("/{id:guid}", async (Guid id, CandidateProfileRequest req, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            var profile = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.Id == id && p.WorkspaceId == ws, ct);
            if (profile is null) return Results.NotFound();
            ApplyUpdate(profile, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(profile.ToResponse());
        });

        // Move the default anchor WITHIN THE WORKSPACE: this profile becomes default, the others are cleared.
        group.MapPost("/{id:guid}/set-default", async (Guid id, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            var target = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.Id == id && p.WorkspaceId == ws, ct);
            if (target is null) return Results.NotFound();

            var others = await db.CandidateProfiles.Where(p => p.WorkspaceId == ws && p.Id != id && p.IsDefault).ToListAsync(ct);
            foreach (var p in others) p.SetDefault(false);
            target.SetDefault(true);
            await db.SaveChangesAsync(ct);
            return Results.Ok(target.ToResponse());
        });
    }

    private static CandidateProfile FromRequest(CandidateProfileRequest req) =>
        new(req.FullName, req.Headline, req.Summary, req.Location, req.Seniority, req.PreferredLanguage,
            req.CoreSkills, req.SecondarySkills, req.Domains, req.PreferredRoles,
            req.PreferredContractTypes, req.PreferredLocations,
            req.Experiences?.Select(e => e.ToDomain()),
            req.DisplayName, req.ExcludedStacks, req.PreferredWorkModes,
            req.MinimumScoreToShow ?? 60);

    private static void ApplyUpdate(CandidateProfile profile, CandidateProfileRequest req) =>
        profile.Update(
            req.FullName, req.Headline, req.Summary, req.Location, req.Seniority, req.PreferredLanguage,
            req.CoreSkills, req.SecondarySkills, req.Domains, req.PreferredRoles,
            req.PreferredContractTypes, req.PreferredLocations,
            req.Experiences?.Select(e => e.ToDomain()),
            req.DisplayName, req.ExcludedStacks, req.PreferredWorkModes, req.MinimumScoreToShow);
}
