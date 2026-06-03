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

        // Internal dev command: seed companies observed manually on LinkedIn into the radar
        // (no new feature/entity; reuses Company + the existing discovery flow). Idempotent.
        group.MapPost("/seed-observed", async (OpportunityOsDbContext db, CancellationToken ct) =>
            Results.Ok(await ObservedCompaniesSeed.RunAsync(db, ct)));

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
            if (ApplyDetection(company, r) != "none") await db.SaveChangesAsync(ct);
            return Results.Ok(new AtsDetectionResponse(
                r.Detected, r.Ats, r.BoardUrl, r.Token, r.CareersPageUrl, r.ProviderSupported));
        });

        // Bulk careers/ATS discovery (the real bottleneck): crawl every company that has a website
        // but no careers/ATS yet, save the ATS board when found — otherwise the careers page so the
        // generic crawler can mine it. Bounded by limit; run in batches. Registers an ExecutionRun.
        group.MapPost("/detect-ats-bulk", async (
            int? limit, OpportunityOsDbContext db, IAtsDetector detector, CancellationToken ct) =>
        {
            var max = Math.Clamp(limit ?? 40, 1, 200);
            var run = ExecutionRun.Start("DetectAtsBulk");
            await db.ExecutionRuns.AddAsync(run, ct);

            var targets = await db.Companies
                .Where(c => c.WebsiteUrl != null && c.WebsiteUrl != "" && (c.CareersUrl == null || c.CareersUrl == ""))
                .OrderByDescending(c => c.Priority).ThenBy(c => c.LastScannedAtUtc)
                .Take(max).ToListAsync(ct);

            int boards = 0, careers = 0, errors = 0;
            foreach (var c in targets)
            {
                try
                {
                    var cat = ApplyDetection(c, await detector.DetectAsync(c, ct));
                    if (cat == "board") boards++; else if (cat == "careers") careers++;
                    run.RecordSuccess();
                }
                catch (Exception ex) { errors++; run.RecordFailure($"{c.Name}: {ex.Message}"); }
            }
            run.Complete();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { processed = targets.Count, boardsFound = boards, careersFound = careers, errors });
        });

        // Onboard companies missing a board: find ATS board (CSE-ATS or heuristic) — chains the funnel.
        group.MapPost("/onboard", async (
            int? limit, ICompanyOnboardingService onboarding, CancellationToken ct) =>
        {
            var r = await onboarding.OnboardAsync(Math.Clamp(limit ?? 25, 1, 200), ct);
            return Results.Ok(new OnboardingResponse(
                r.ExecutionRunId, r.Status, r.Processed, r.BoardsFound, r.Errors));
        });

        // Backfill websites for companies that have a match but no website yet (for logos).
        group.MapPost("/backfill-websites", async (
            int? limit, OpportunityOsDbContext db, ICompanyWebsiteDiscoverer discoverer, CancellationToken ct) =>
        {
            var max = Math.Clamp(limit ?? 20, 1, 200);
            var matchedJobIds = await db.OpportunityMatches.Select(m => m.JobPostingId).Distinct().ToListAsync(ct);
            var companyIds = await db.JobPostings.Where(j => matchedJobIds.Contains(j.Id))
                .Select(j => j.CompanyId).Distinct().ToListAsync(ct);
            var targets = await db.Companies
                .Where(c => companyIds.Contains(c.Id) && (c.WebsiteUrl == null || c.WebsiteUrl == ""))
                .Take(max).ToListAsync(ct);

            var found = 0;
            foreach (var c in targets)
            {
                var r = await discoverer.DiscoverAsync(c.Name, ct);
                if (r is { Found: true, WebsiteUrl: { Length: > 0 } url }) { c.SetWebsiteUrl(url); found++; }
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new BackfillResponse(targets.Count, found));
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

    /// <summary>Persist a detection result on a company: prefer the ATS board (enables the good
    /// providers); otherwise save the discovered careers page so the generic crawler can mine it.
    /// Returns "board" | "careers" | "none".</summary>
    private static string ApplyDetection(Company company, AtsDetectionResult r)
    {
        if (r.Detected && !string.IsNullOrWhiteSpace(r.BoardUrl))
        {
            company.SetCareersUrl(r.BoardUrl!);
            if (r.Ats is not null) company.AddTag(r.Ats.ToLowerInvariant());
            company.AddTag("ats-detected");
            return "board";
        }
        if (!string.IsNullOrWhiteSpace(r.CareersPageUrl))
        {
            company.SetCareersUrl(r.CareersPageUrl!);
            company.AddTag("careers-page");
            return "careers";
        }
        return "none";
    }
}
