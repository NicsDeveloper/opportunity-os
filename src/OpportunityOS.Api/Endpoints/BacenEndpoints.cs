using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Bacen;
using OpportunityOS.Contracts;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class BacenEndpoints
{
    public static void MapBacenEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bacen/pix-participants").WithTags("Bacen").RequireAuthorization("System");

        // Import the official CSV into BacenInstitution (raw radar).
        group.MapPost("/import", async (IBacenRadarService svc, CancellationToken ct) =>
        {
            var r = await svc.ImportPixParticipantsAsync(ct);
            return Results.Ok(new BacenImportResponse(r.TotalRead, r.Created, r.Updated, r.Skipped, r.Warnings));
        });

        // Promote eligible institutions into the Company radar (curated).
        group.MapPost("/promote-to-companies", async (IBacenRadarService svc, CancellationToken ct) =>
        {
            var r = await svc.PromotePixParticipantsToCompaniesAsync(ct);
            return Results.Ok(new BacenPromotionResponse(
                r.TotalEligible, r.CompaniesCreated, r.CompaniesUpdated, r.Skipped, r.Warnings));
        });

        // List imported institutions with filters.
        group.MapGet("/", async (
            string? institutionType, bool? authorizedByBacen, string? tag, string? search,
            OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var query = db.BacenInstitutions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(institutionType))
                query = query.Where(i => i.InstitutionType.ToLower().Contains(institutionType.ToLower()));
            if (authorizedByBacen is { } auth)
                query = query.Where(i => i.AuthorizedByBacen == auth);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(i => i.Name.ToLower().Contains(search.ToLower()));

            var items = await query.OrderBy(i => i.Name).Take(500).ToListAsync(ct);

            // Tag filter applied in-memory (Tags is a jsonb list).
            if (!string.IsNullOrWhiteSpace(tag))
                items = items.Where(i => i.Tags.Contains(tag)).ToList();

            return Results.Ok(items.Select(i => i.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var inst = await db.BacenInstitutions.FindAsync([id], ct);
            return inst is null ? Results.NotFound() : Results.Ok(inst.ToResponse());
        });
    }
}
