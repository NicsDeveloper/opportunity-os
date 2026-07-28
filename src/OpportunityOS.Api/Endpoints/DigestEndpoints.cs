using OpportunityOS.Application.Digest;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Contracts;

namespace OpportunityOS.Api.Endpoints;

public static class DigestEndpoints
{
    public static void MapDigestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/digest").WithTags("Digest").RequireAuthorization();

        // Render the digest (for the requested or default profile) without sending anything.
        group.MapGet("/preview", async (
            int? minScore, Guid? candidateProfileId, IEmailDigestService digest,
            ICurrentCandidateProfileProvider profiles, CancellationToken ct) =>
        {
            var profileId = await profiles.ResolveIdAsync(candidateProfileId, ct);
            if (profileId is null)
                return Results.Ok(new DigestPreviewResponse("", "", "", 0, 0, 0, 0));
            var p = await digest.BuildPreviewAsync(profileId.Value, minScore ?? EmailDigestService.DefaultMinScore, ct);
            return Results.Ok(new DigestPreviewResponse(
                p.Subject, p.Markdown, p.Html, p.Total, p.StrategicCount, p.PrioritizeCount, p.ApplyCount));
        });

        // Explicit send for one profile (user-triggered). Records an ExecutionRun; never sends attachments.
        group.MapPost("/send", async (
            int? minScore, Guid? candidateProfileId, IEmailDigestService digest,
            ICurrentCandidateProfileProvider profiles, CancellationToken ct) =>
        {
            var profileId = await profiles.ResolveIdAsync(candidateProfileId, ct);
            if (profileId is null)
                return Results.Ok(new DigestSendResponse(false, "No candidate profile registered.", 0, Guid.Empty));
            var r = await digest.SendDailyDigestAsync(profileId.Value, minScore ?? EmailDigestService.DefaultMinScore, ct);
            return Results.Ok(new DigestSendResponse(r.Sent, r.Reason, r.ItemCount, r.ExecutionRunId));
        });

        // Send a digest for every profile (what the daily job does). One result per profile.
        // System-level: it operates across ALL workspaces, so it is not a personal action.
        group.MapPost("/send-all", async (IEmailDigestService digest, CancellationToken ct) =>
        {
            var results = await digest.SendAllAsync(ct);
            return Results.Ok(results.Select(r =>
                new DigestSendResponse(r.Sent, r.Reason, r.ItemCount, r.ExecutionRunId)).ToList());
        }).RequireAuthorization("System");
    }
}
