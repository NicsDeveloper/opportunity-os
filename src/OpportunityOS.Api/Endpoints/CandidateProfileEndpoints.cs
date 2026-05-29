using Microsoft.EntityFrameworkCore;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class CandidateProfileEndpoints
{
    public static void MapCandidateProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/candidate-profile").WithTags("CandidateProfile");

        // Phase 1 keeps a single active profile: GET returns the most recent.
        group.MapGet("/", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = await db.CandidateProfiles
                .OrderByDescending(p => p.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToResponse());
        });

        group.MapGet("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = await db.CandidateProfiles.FindAsync([id], ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToResponse());
        });

        group.MapPost("/", async (CandidateProfileRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = new CandidateProfile(
                req.FullName, req.Headline, req.Summary, req.Location, req.Seniority, req.PreferredLanguage,
                req.CoreSkills, req.SecondarySkills, req.Domains, req.PreferredRoles,
                req.PreferredContractTypes, req.PreferredLocations,
                req.Experiences?.Select(e => e.ToDomain()));
            db.CandidateProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/candidate-profile/{profile.Id}", profile.ToResponse());
        });

        group.MapPut("/{id:guid}", async (Guid id, CandidateProfileRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var profile = await db.CandidateProfiles.FindAsync([id], ct);
            if (profile is null) return Results.NotFound();
            profile.Update(
                req.FullName, req.Headline, req.Summary, req.Location, req.Seniority, req.PreferredLanguage,
                req.CoreSkills, req.SecondarySkills, req.Domains, req.PreferredRoles,
                req.PreferredContractTypes, req.PreferredLocations,
                req.Experiences?.Select(e => e.ToDomain()));
            await db.SaveChangesAsync(ct);
            return Results.Ok(profile.ToResponse());
        });
    }
}
