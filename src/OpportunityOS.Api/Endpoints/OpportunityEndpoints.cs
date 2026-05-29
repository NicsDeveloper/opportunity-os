using Microsoft.EntityFrameworkCore;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class OpportunityEndpoints
{
    public static void MapOpportunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/opportunities").WithTags("Opportunities");

        group.MapGet("/", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var items = await db.Opportunities
                .OrderByDescending(o => o.CreatedAtUtc)
                .ToListAsync(ct);
            return Results.Ok(items.Select(o => o.ToResponse()));
        });

        // Pending follow-ups (due now or earlier). Declared before {id} to avoid route clash.
        group.MapGet("/follow-ups", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var due = await db.Opportunities
                .Where(o => o.NextFollowUpAtUtc != null && o.NextFollowUpAtUtc <= now)
                .OrderBy(o => o.NextFollowUpAtUtc)
                .ToListAsync(ct);
            return Results.Ok(due.Select(o => o.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var o = await db.Opportunities.FindAsync([id], ct);
            return o is null ? Results.NotFound() : Results.Ok(o.ToResponse());
        });

        // Manual status change — the only path allowed to set human-gated statuses.
        group.MapPut("/{id:guid}/status", async (
            Guid id, OpportunityStatusRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (!Enum.TryParse<OpportunityStatus>(req.Status, ignoreCase: true, out var status))
                return Results.BadRequest($"Invalid status '{req.Status}'.");

            var o = await db.Opportunities.FindAsync([id], ct);
            if (o is null) return Results.NotFound();
            o.SetStatusManually(status);
            await db.SaveChangesAsync(ct);
            return Results.Ok(o.ToResponse());
        });

        group.MapPut("/{id:guid}/notes", async (
            Guid id, OpportunityNotesRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var o = await db.Opportunities.FindAsync([id], ct);
            if (o is null) return Results.NotFound();
            o.SetNotes(req.Notes);
            await db.SaveChangesAsync(ct);
            return Results.Ok(o.ToResponse());
        });

        group.MapPut("/{id:guid}/follow-up", async (
            Guid id, OpportunityFollowUpRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var o = await db.Opportunities.FindAsync([id], ct);
            if (o is null) return Results.NotFound();
            o.SetFollowUp(req.NextFollowUpAtUtc);
            await db.SaveChangesAsync(ct);
            return Results.Ok(o.ToResponse());
        });
    }
}
