using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Application.Matching;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Application.Pipeline;
using OpportunityOS.Contracts;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs");

        group.MapGet("/", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var jobs = await db.JobPostings
                .OrderByDescending(j => j.CreatedAtUtc)
                .ToListAsync(ct);
            return Results.Ok(jobs.Select(j => j.ToResponse()));
        });

        group.MapGet("/{id:guid}", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var job = await db.JobPostings.FindAsync([id], ct);
            return job is null ? Results.NotFound() : Results.Ok(job.ToResponse());
        });

        // Manual discovery run: pull public postings from ATS providers and persist
        // them (dedup by SourceProvider + ExternalId). Optionally scoped to one company.
        group.MapPost("/discover", async (
            DiscoverRequest? req, IJobDiscoveryService discovery, CancellationToken ct) =>
        {
            var result = await discovery.DiscoverAsync(req?.CompanyId, ct);
            return Results.Ok(new DiscoveryResultResponse(
                result.ExecutionRunId, result.Status, result.CompaniesProcessed,
                result.ProvidersInvoked, result.JobsDiscovered, result.JobsUpdated, result.Errors));
        });

        // Keyword search across search providers (Gupy). Defaults to a sensible
        // .NET/payments keyword set when none is supplied.
        group.MapPost("/search", async (
            SearchRequest? req, IJobDiscoveryService discovery, CancellationToken ct) =>
        {
            var keywords = req?.Keywords is { Count: > 0 } k
                ? k
                : new List<string> { ".net", "c#", "desenvolvedor .net", "backend", "pagamentos" };
            var result = await discovery.SearchAsync(keywords, ct);
            return Results.Ok(new DiscoveryResultResponse(
                result.ExecutionRunId, result.Status, result.CompaniesProcessed,
                result.ProvidersInvoked, result.JobsDiscovered, result.JobsUpdated, result.Errors));
        });

        // Manual analysis run: normalize + score against the active profile.
        group.MapPost("/{id:guid}/match", async (
            Guid id, OpportunityOsDbContext db, IJobNormalizer normalizer, IMatchEngine engine,
            IOpportunityPipeline pipeline, CancellationToken ct) =>
        {
            var job = await db.JobPostings.FindAsync([id], ct);
            if (job is null) return Results.NotFound();

            var profile = await db.CandidateProfiles
                .OrderByDescending(p => p.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (profile is null)
                return Results.BadRequest("No candidate profile registered. Create one first.");

            var norm = normalizer.Normalize(job);
            job.ApplyNormalization(norm.Seniority, norm.WorkMode, norm.Language, norm.Skills, norm.Domains);

            var result = engine.Evaluate(profile, job);
            var match = result.ToEntity(job.Id, profile.Id);
            job.MarkAnalyzed();
            db.OpportunityMatches.Add(match);
            await db.SaveChangesAsync(ct);

            // Auto-create an Opportunity when the score qualifies (score >= 70).
            await pipeline.EnsureForMatchAsync(match, ct);

            return Results.Ok(match.ToResponse());
        });

        // Latest match for a job, if any.
        group.MapGet("/{id:guid}/match", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var match = await db.OpportunityMatches
                .Where(m => m.JobPostingId == id)
                .OrderByDescending(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            return match is null ? Results.NotFound() : Results.Ok(match.ToResponse());
        });

        // Validate posting links and expire dead ones (404/410), so stale vagas drop off.
        group.MapPost("/validate-links", async (
            int? limit, IJobLinkValidator validator, CancellationToken ct) =>
        {
            var r = await validator.ValidateAsync(limit ?? 100, ct);
            return Results.Ok(new ValidateLinksResponse(r.Checked, r.Expired));
        });

        group.MapPost("/{id:guid}/archive", async (Guid id, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var job = await db.JobPostings.FindAsync([id], ct);
            if (job is null) return Results.NotFound();
            job.Archive();
            await db.SaveChangesAsync(ct);
            return Results.Ok(job.ToResponse());
        });
    }
}
