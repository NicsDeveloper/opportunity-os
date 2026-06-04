using Microsoft.EntityFrameworkCore;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>
/// Dev/validation endpoints — not part of the product surface. Lets a human confirm the
/// engine reasons DIFFERENTLY per profile for the same global job.
/// </summary>
public static class DebugEndpoints
{
    public static void MapDebugEndpoints(this IEndpointRouteBuilder app)
    {
        // Latest score/recommendation/engineVersion of one job across every candidate profile.
        app.MapGet("/api/debug/job/{jobId:guid}/profile-scores", async (
            Guid jobId, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var job = await db.JobPostings.FindAsync([jobId], ct);
            if (job is null) return Results.NotFound();

            var profiles = await db.CandidateProfiles
                .OrderByDescending(p => p.IsDefault).ThenBy(p => p.DisplayName)
                .Select(p => new { p.Id, p.DisplayName })
                .ToListAsync(ct);

            var matches = await db.OpportunityMatches
                .Where(m => m.JobPostingId == jobId)
                .ToListAsync(ct);
            var latestByProfile = matches
                .GroupBy(m => m.CandidateProfileId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAtUtc).First());

            var scores = profiles.Select(p =>
            {
                latestByProfile.TryGetValue(p.Id, out var m);
                return new
                {
                    candidateProfileId = p.Id,
                    candidateProfile = p.DisplayName,
                    score = (int?)m?.OverallScore,
                    recommendation = m?.Recommendation.ToString(),
                    engineVersion = m?.EngineVersion,
                    createdAtUtc = (DateTime?)m?.CreatedAtUtc
                };
            }).ToList();

            return Results.Ok(new { jobId, jobTitle = job.Title, scores });
        }).WithTags("Debug");
    }
}
