using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Companies;
using OpportunityOS.Application.Discovery;
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

        // Detect the company's ATS by crawling its public site/careers page.
        group.MapPost("/{id:guid}/detect-ats", async (
            Guid id, OpportunityOsDbContext db, IAtsDetector detector, CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([id], ct);
            if (company is null) return Results.NotFound();

            var r = await detector.DetectAsync(company, ct);
            if (r.Detected && !string.IsNullOrWhiteSpace(r.BoardUrl))
            {
                company.SetCareersUrl(r.BoardUrl!);
                if (r.Ats is not null) company.AddTag(r.Ats.ToLowerInvariant());
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new AtsDetectionResponse(
                r.Detected, r.Ats, r.BoardUrl, r.Token, r.CareersPageUrl, r.ProviderSupported));
        });

        // Onboard companies missing a website: discover site -> detect ATS (chains the funnel).
        group.MapPost("/onboard", async (
            int? limit, ICompanyOnboardingService onboarding, CancellationToken ct) =>
        {
            var r = await onboarding.OnboardAsync(Math.Clamp(limit ?? 25, 1, 200), ct);
            return Results.Ok(new OnboardingResponse(
                r.ExecutionRunId, r.Status, r.Processed, r.WebsitesFound, r.AtsDetected, r.Errors));
        });

        // Discover a single company's website from its name (no persistence beyond setting it).
        group.MapPost("/{id:guid}/discover-website", async (
            Guid id, OpportunityOsDbContext db, ICompanyWebsiteDiscoverer discoverer, CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([id], ct);
            if (company is null) return Results.NotFound();
            var r = await discoverer.DiscoverAsync(company.Name, ct);
            if (r is { Found: true, WebsiteUrl: { Length: > 0 } url })
            {
                company.SetWebsiteUrl(url);
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new WebsiteDiscoveryResponse(r.Found, r.WebsiteUrl));
        });

        // Bulk import companies from CSV (name,websiteUrl,careersUrl,industry,country).
        group.MapPost("/import-csv", async (HttpRequest request, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var csv = await reader.ReadToEndAsync(ct);
            var rows = CsvCompanyParser.Parse(csv);
            if (rows.Count == 0) return Results.BadRequest("No valid rows found (expected at least a 'name' column).");

            var created = 0;
            foreach (var row in rows)
            {
                if (await db.Companies.AnyAsync(c => c.Name.ToLower() == row.Name.ToLower(), ct)) continue;
                db.Companies.Add(new Company(
                    row.Name, row.WebsiteUrl, row.CareersUrl, null, row.Industry, row.Country,
                    CompanyPriority.Medium, CompanySource.CsvImport, null));
                created++;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CsvImportResponse(created));
        });
    }
}
