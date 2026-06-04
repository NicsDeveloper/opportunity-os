using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>User feedback (P11): relevant/irrelevant/bad-company/duplicate/applied/...</summary>
public static class FeedbackEndpoints
{
    public static void MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback").WithTags("Feedback").RequireAuthorization();

        group.MapPost("/", async (
            FeedbackRequest req, OpportunityOsDbContext db,
            ICurrentCandidateProfileProvider profiles, CancellationToken ct) =>
        {
            if (!Enum.TryParse<UserFeedbackType>(req.Type, ignoreCase: true, out var type))
                return Results.BadRequest($"Invalid feedback type '{req.Type}'.");
            if (req.JobPostingId is null && req.RawJobCandidateId is null)
                return Results.BadRequest("Provide JobPostingId or RawJobCandidateId.");

            // Feedback is personal to a profile (defaults to the current/default profile).
            var profileId = await profiles.ResolveIdAsync(req.CandidateProfileId, ct);
            var feedback = new UserFeedback(type, req.JobPostingId, req.RawJobCandidateId, req.Reason, profileId);
            db.UserFeedbacks.Add(feedback);

            // Dead-link signal: if the user says the posting is closed/moved/gone, expire it so it
            // (and any rediscovery) drops out of the board.
            if (req.JobPostingId is { } jid && (type == UserFeedbackType.Expired || LooksDead(req.Reason)))
            {
                var job = await db.JobPostings.FindAsync([jid], ct);
                job?.MarkExpired();
            }

            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/feedback/{feedback.Id}", new { feedback.Id, Type = type.ToString(), feedback.CreatedAtUtc });
        });

        // List feedback for THIS workspace. With candidateProfileId, only that (owned) profile's
        // feedback; otherwise every profile in the caller's workspace. Never another user's.
        group.MapGet("/", async (
            int? take, Guid? candidateProfileId, ICurrentUserContext user,
            ICurrentCandidateProfileProvider profiles, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var limit = Math.Clamp(take ?? 100, 1, 500);

            List<Guid> profileIds;
            if (candidateProfileId is not null)
            {
                // Throws (→403) if the profile isn't in the caller's workspace.
                var resolved = await profiles.ResolveIdAsync(candidateProfileId, ct);
                profileIds = resolved is { } r ? new List<Guid> { r } : new();
            }
            else
            {
                var ws = await user.GetWorkspaceIdAsync(ct);
                profileIds = await db.CandidateProfiles.Where(p => p.WorkspaceId == ws).Select(p => p.Id).ToListAsync(ct);
            }

            var items = await db.UserFeedbacks
                .Where(f => f.CandidateProfileId != null && profileIds.Contains(f.CandidateProfileId!.Value))
                .OrderByDescending(f => f.CreatedAtUtc).Take(limit)
                .Select(f => new { f.Id, Type = f.Type.ToString(), f.JobPostingId, f.RawJobCandidateId, f.CandidateProfileId, f.Reason, f.CreatedAtUtc })
                .ToListAsync(ct);
            return Results.Ok(items);
        });
    }

    private static bool LooksDead(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return false;
        var r = reason.ToLowerInvariant();
        return r.Contains("encerrad") || r.Contains("expirad") || r.Contains("movida")
            || r.Contains("não existe") || r.Contains("nao existe") || r.Contains("fora do ar")
            || r.Contains("removida") || r.Contains("não está mais") || r.Contains("nao esta mais");
    }
}
