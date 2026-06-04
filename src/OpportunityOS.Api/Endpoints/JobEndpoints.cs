using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Application.Matching;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Application.Pipeline;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Application.Projections;
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

        // Manual analysis run: normalize + score against the requested (or default) profile.
        group.MapPost("/{id:guid}/match", async (
            Guid id, Guid? candidateProfileId, OpportunityOsDbContext db,
            ICurrentCandidateProfileProvider profiles, IJobNormalizer normalizer, IMatchEngine engine,
            ILatestOpportunityMatchProjection latest, IOpportunityPipeline pipeline, CancellationToken ct) =>
        {
            var job = await db.JobPostings.FindAsync([id], ct);
            if (job is null) return Results.NotFound();

            var profile = await profiles.GetAsync(candidateProfileId, ct);
            if (profile is null)
                return Results.BadRequest("No candidate profile registered. Create one first.");

            var norm = normalizer.Normalize(job);
            job.ApplyNormalization(norm.Seniority, norm.WorkMode, norm.Language, norm.Skills, norm.Domains);

            var result = engine.Evaluate(profile, job);
            var match = result.ToEntity(job.Id, profile.Id);
            job.MarkAnalyzed();
            db.OpportunityMatches.Add(match);
            await latest.UpsertAsync(match, ct);
            await db.SaveChangesAsync(ct);

            // Auto-create an Opportunity when the score qualifies (score >= 70).
            await pipeline.EnsureForMatchAsync(match, ct);

            return Results.Ok(match.ToResponse());
        });

        // Re-score active jobs with the current engine. By default ONLY the resolved/default profile
        // is rescored; pass allProfiles=true to fan out to every profile (heavier). take caps the job
        // set; engineVersion tags the produced matches. Writes a fresh match only when the score changed.
        group.MapPost("/rescore", async (
            Guid? candidateProfileId, bool? allProfiles, int? take, string? engineVersion,
            bool? onlyWithoutCurrentEngineVersion, DateTime? minCreatedAtUtc,
            OpportunityOsDbContext db, ICurrentCandidateProfileProvider profiles,
            IJobNormalizer normalizer, IMatchEngine engine,
            ILatestOpportunityMatchProjection projection, CancellationToken ct) =>
        {
            List<CandidateProfile> targets;
            if (allProfiles == true)
            {
                targets = await db.CandidateProfiles.ToListAsync(ct);
            }
            else
            {
                var profile = await profiles.GetAsync(candidateProfileId, ct);
                if (profile is null) return Results.BadRequest("No candidate profile registered.");
                targets = new List<CandidateProfile> { profile };
            }
            if (targets.Count == 0) return Results.BadRequest("No candidate profile registered.");
            var version = string.IsNullOrWhiteSpace(engineVersion) ? HeuristicMatchEngine.Version : engineVersion!;
            var skipCurrent = onlyWithoutCurrentEngineVersion == true;

            var run = ExecutionRun.Start("RescoreMatches");
            await db.ExecutionRuns.AddAsync(run, ct);

            var jobsQuery = db.JobPostings
                .Where(j => j.Status != JobPostingStatus.Expired && j.Status != JobPostingStatus.Archived);
            if (minCreatedAtUtc is { } since) jobsQuery = jobsQuery.Where(j => j.CreatedAtUtc >= since);
            var orderedJobs = jobsQuery.OrderByDescending(j => j.CreatedAtUtc);
            var jobs = take is { } t
                ? await orderedJobs.Take(Math.Clamp(t, 1, 100000)).ToListAsync(ct)
                : await orderedJobs.ToListAsync(ct);

            // Latest match per (job, profile): its score (skip unchanged) and engine version
            // (skip already-current when onlyWithoutCurrentEngineVersion=true).
            var pids = targets.Select(p => p.Id).ToList();
            var existing = await db.OpportunityMatches
                .Where(m => pids.Contains(m.CandidateProfileId))
                .Select(m => new { m.JobPostingId, m.CandidateProfileId, m.OverallScore, m.EngineVersion, m.CreatedAtUtc })
                .ToListAsync(ct);
            var latest = existing
                .GroupBy(m => (m.JobPostingId, m.CandidateProfileId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAtUtc).First());

            // Preload the target profiles' projection rows into the unit of work so the per-match
            // upsert resolves them locally (no per-match round-trip).
            await db.LatestOpportunityMatches.Where(p => pids.Contains(p.CandidateProfileId)).LoadAsync(ct);

            int rescored = 0, changed = 0, skipped = 0;
            foreach (var job in jobs)
            {
                var norm = normalizer.Normalize(job);
                job.ApplyNormalization(norm.Seniority, norm.WorkMode, norm.Language, norm.Skills, norm.Domains);
                foreach (var profile in targets)
                {
                    var hasLatest = latest.TryGetValue((job.Id, profile.Id), out var prev);
                    if (skipCurrent && hasLatest && prev!.EngineVersion == version) { skipped++; continue; }
                    var result = engine.Evaluate(profile, job);
                    rescored++;
                    if (hasLatest && prev!.OverallScore == result.OverallScore && prev.EngineVersion == version) continue;
                    var newMatch = result.ToEntity(job.Id, profile.Id, version);
                    db.OpportunityMatches.Add(newMatch);
                    await projection.UpsertAsync(newMatch, ct);
                    changed++;
                    run.RecordSuccess();
                }
            }
            run.Complete();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { profiles = targets.Count, rescored, changed, skipped });
        });

        // Rebuild the LatestOpportunityMatch projection from the existing matches (dev/admin repair,
        // e.g. right after the migration). Idempotent.
        group.MapPost("/rebuild-latest-matches", async (
            ILatestOpportunityMatchProjection latest, CancellationToken ct) =>
        {
            var r = await latest.BackfillAsync(ct);
            return Results.Ok(new { processedPairs = r.ProcessedPairs, created = r.Created, updated = r.Updated, skipped = r.Skipped });
        });

        // Latest match for a job for the requested (or default) profile.
        group.MapGet("/{id:guid}/match", async (
            Guid id, Guid? candidateProfileId, OpportunityOsDbContext db,
            ICurrentCandidateProfileProvider profiles, CancellationToken ct) =>
        {
            var profileId = await profiles.ResolveIdAsync(candidateProfileId, ct);
            var match = await db.OpportunityMatches
                .Where(m => m.JobPostingId == id && (profileId == null || m.CandidateProfileId == profileId.Value))
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

        // B10 — backfill source quality on jobs discovered before the classifier existed
        // (they show as "Fonte a confirmar"). Classifies by URL; no network/LLM.
        group.MapPost("/backfill-source-quality", async (
            int? limit, OpportunityOsDbContext db, ISourceClassifierService classifier, CancellationToken ct) =>
        {
            var max = Math.Clamp(limit ?? 1000, 1, 10000);
            // Old rows predate the column (stored 0); new unclassified ones are Unknown(7).
            var jobs = await db.JobPostings
                .Where(j => j.SourceType == (SourceType)0 || j.SourceType == SourceType.Unknown)
                .Take(max).ToListAsync(ct);
            foreach (var j in jobs)
            {
                var c = classifier.Classify(j.AbsoluteUrl, j.Title, j.DescriptionText);
                j.SetSourceQuality(c.SourceType, c.SourceName, c.SourceConfidenceScore, c.RequiresManualValidation);
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { updated = jobs.Count });
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
