using Microsoft.EntityFrameworkCore;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class CompanyEndpoints
{
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies").WithTags("Companies");

        // Strategic companies first (the spec wants them scanned first).
        group.MapGet("/", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var companies = await db.Companies
                .OrderByDescending(c => c.Priority)
                .ThenBy(c => c.Name)
                .ToListAsync(ct);
            return Results.Ok(companies.Select(c => c.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([id], ct);
            return company is null ? Results.NotFound() : Results.Ok(company.ToResponse());
        });

        group.MapPost("/", async (CompanyRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (!Enum.IsDefined(typeof(CompanyPriority), req.Priority))
                return Results.BadRequest($"Invalid priority '{req.Priority}'.");

            var company = new Company(
                req.Name, req.WebsiteUrl, req.CareersUrl, req.LinkedInUrl, req.Industry, req.Country,
                (CompanyPriority)req.Priority, CompanySource.Manual, req.Tags);
            db.Companies.Add(company);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/companies/{company.Id}", company.ToResponse());
        });

        group.MapPut("/{id:guid}", async (Guid id, CompanyRequest req, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (!Enum.IsDefined(typeof(CompanyPriority), req.Priority))
                return Results.BadRequest($"Invalid priority '{req.Priority}'.");

            var company = await db.Companies.FindAsync([id], ct);
            if (company is null) return Results.NotFound();
            company.Update(req.Name, req.WebsiteUrl, req.CareersUrl, req.LinkedInUrl, req.Industry,
                req.Country, (CompanyPriority)req.Priority, req.Tags);
            await db.SaveChangesAsync(ct);
            return Results.Ok(company.ToResponse());
        });

        group.MapDelete("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([id], ct);
            if (company is null) return Results.NotFound();
            db.Companies.Remove(company);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
