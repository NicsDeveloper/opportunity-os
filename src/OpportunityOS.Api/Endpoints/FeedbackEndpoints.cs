using Microsoft.EntityFrameworkCore;
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
        var group = app.MapGroup("/api/feedback").WithTags("Feedback");

        group.MapPost("/", async (FeedbackRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (!Enum.TryParse<UserFeedbackType>(req.Type, ignoreCase: true, out var type))
                return Results.BadRequest($"Invalid feedback type '{req.Type}'.");
            if (req.JobPostingId is null && req.RawJobCandidateId is null)
                return Results.BadRequest("Provide JobPostingId or RawJobCandidateId.");

            var feedback = new UserFeedback(type, req.JobPostingId, req.RawJobCandidateId, req.Reason);
            db.UserFeedbacks.Add(feedback);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/feedback/{feedback.Id}", new { feedback.Id, Type = type.ToString(), feedback.CreatedAtUtc });
        });

        group.MapGet("/", async (int? take, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var limit = Math.Clamp(take ?? 100, 1, 500);
            var items = await db.UserFeedbacks
                .OrderByDescending(f => f.CreatedAtUtc).Take(limit)
                .Select(f => new { f.Id, Type = f.Type.ToString(), f.JobPostingId, f.RawJobCandidateId, f.Reason, f.CreatedAtUtc })
                .ToListAsync(ct);
            return Results.Ok(items);
        });
    }
}
