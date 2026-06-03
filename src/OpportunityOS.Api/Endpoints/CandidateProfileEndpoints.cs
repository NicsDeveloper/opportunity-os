using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class CandidateProfileEndpoints
{
    public static void MapCandidateProfileEndpoints(this IEndpointRouteBuilder app)
    {
        MapSingular(app);
        MapPlural(app);
    }

    // Back-compat: the original singular route returns the CURRENT (default/most-recent) profile.
    private static void MapSingular(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/candidate-profile").WithTags("CandidateProfile");

        group.MapGet("/", async (ICurrentCandidateProfileProvider provider, CancellationToken ct) =>
        {
            var profile = await provider.GetAsync(null, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToResponse());
        });

        group.MapGet("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = await db.CandidateProfiles.FindAsync([id], ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToResponse());
        });

        group.MapPost("/", async (CandidateProfileRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = FromRequest(req);
            db.CandidateProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/candidate-profiles/{profile.Id}", profile.ToResponse());
        });

        group.MapPut("/{id:guid}", async (Guid id, CandidateProfileRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = await db.CandidateProfiles.FindAsync([id], ct);
            if (profile is null) return Results.NotFound();
            ApplyUpdate(profile, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(profile.ToResponse());
        });
    }

    // Multi-profile collection API.
    private static void MapPlural(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/candidate-profiles").WithTags("CandidateProfile");

        group.MapGet("/", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profiles = await db.CandidateProfiles
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.DisplayName)
                .ToListAsync(ct);
            return Results.Ok(profiles.Select(p => p.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = await db.CandidateProfiles.FindAsync([id], ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToResponse());
        });

        group.MapPost("/", async (CandidateProfileRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = FromRequest(req);
            // The first profile created becomes the default anchor automatically.
            if (!await db.CandidateProfiles.AnyAsync(ct)) profile.SetDefault(true);
            db.CandidateProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/candidate-profiles/{profile.Id}", profile.ToResponse());
        });

        group.MapPut("/{id:guid}", async (Guid id, CandidateProfileRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = await db.CandidateProfiles.FindAsync([id], ct);
            if (profile is null) return Results.NotFound();
            ApplyUpdate(profile, req);
            await db.SaveChangesAsync(ct);
            return Results.Ok(profile.ToResponse());
        });

        // Move the default anchor: this profile becomes default, every other one is cleared.
        group.MapPost("/{id:guid}/set-default", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var target = await db.CandidateProfiles.FindAsync([id], ct);
            if (target is null) return Results.NotFound();

            var others = await db.CandidateProfiles.Where(p => p.Id != id && p.IsDefault).ToListAsync(ct);
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
