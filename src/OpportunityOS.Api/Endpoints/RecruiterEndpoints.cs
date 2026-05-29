using Microsoft.EntityFrameworkCore;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class RecruiterEndpoints
{
    public static void MapRecruiterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recruiters").WithTags("Recruiters");

        group.MapGet("/", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var items = await db.RecruiterLeads
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToListAsync(ct);
            return Results.Ok(items.Select(r => r.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var r = await db.RecruiterLeads.FindAsync([id], ct);
            return r is null ? Results.NotFound() : Results.Ok(r.ToResponse());
        });

        group.MapPost("/", async (RecruiterRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var source = req.Source is { } s && Enum.IsDefined(typeof(RecruiterLeadSource), s)
                ? (RecruiterLeadSource)s
                : RecruiterLeadSource.Manual;

            if (!await db.Companies.AnyAsync(c => c.Id == req.CompanyId, ct))
                return Results.BadRequest($"Company {req.CompanyId} not found.");

            var lead = new RecruiterLead(
                req.CompanyId, req.FullName, req.RoleTitle, req.LinkedInUrl, req.Email, source, req.Notes);
            db.RecruiterLeads.Add(lead);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/recruiters/{lead.Id}", lead.ToResponse());
        });

        group.MapPut("/{id:guid}", async (
            Guid id, RecruiterRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var lead = await db.RecruiterLeads.FindAsync([id], ct);
            if (lead is null) return Results.NotFound();
            lead.Update(req.FullName, req.RoleTitle, req.LinkedInUrl, req.Email, req.Notes);
            await db.SaveChangesAsync(ct);
            return Results.Ok(lead.ToResponse());
        });

        group.MapDelete("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var lead = await db.RecruiterLeads.FindAsync([id], ct);
            if (lead is null) return Results.NotFound();
            db.RecruiterLeads.Remove(lead);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
