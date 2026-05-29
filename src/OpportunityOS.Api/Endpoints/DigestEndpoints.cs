using OpportunityOS.Application.Digest;
using OpportunityOS.Contracts;

namespace OpportunityOS.Api.Endpoints;

public static class DigestEndpoints
{
    public static void MapDigestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/digest").WithTags("Digest");

        // Render the digest without sending anything.
        group.MapGet("/preview", async (
            int? minScore, IEmailDigestService digest, CancellationToken ct) =>
        {
            var p = await digest.BuildPreviewAsync(minScore ?? EmailDigestService.DefaultMinScore, ct);
            return Results.Ok(new DigestPreviewResponse(
                p.Subject, p.Markdown, p.Html, p.Total, p.StrategicCount, p.PrioritizeCount, p.ApplyCount));
        });

        // Explicit send (user-triggered). Records an ExecutionRun; never sends attachments.
        group.MapPost("/send", async (
            int? minScore, IEmailDigestService digest, CancellationToken ct) =>
        {
            var r = await digest.SendDailyDigestAsync(minScore ?? EmailDigestService.DefaultMinScore, ct);
            return Results.Ok(new DigestSendResponse(r.Sent, r.Reason, r.ItemCount, r.ExecutionRunId));
        });
    }
}
