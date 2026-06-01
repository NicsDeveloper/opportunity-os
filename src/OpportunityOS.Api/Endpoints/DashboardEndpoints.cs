using Microsoft.EntityFrameworkCore;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // Aggregate counts + "today" deltas for the dashboard cards.
        app.MapGet("/api/dashboard/summary", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var today = DateTime.UtcNow.Date;
            var now = DateTime.UtcNow;

            var jobs = await db.JobPostings.CountAsync(ct);
            var jobsToday = await db.JobPostings.CountAsync(j => j.CreatedAtUtc >= today, ct);

            var matches75 = await db.OpportunityMatches.CountAsync(m => m.OverallScore >= 75, ct);
            var matches75Today = await db.OpportunityMatches.CountAsync(m => m.OverallScore >= 75 && m.CreatedAtUtc >= today, ct);

            var messages = await db.GeneratedMessages.CountAsync(ct);
            var messagesToday = await db.GeneratedMessages.CountAsync(m => m.CreatedAtUtc >= today, ct);

            var emails = await db.ExecutionRuns.CountAsync(
                r => r.RunType == "SendDailyDigest" && r.ItemsSucceeded > 0, ct);
            var emailsToday = await db.ExecutionRuns.CountAsync(
                r => r.RunType == "SendDailyDigest" && r.ItemsSucceeded > 0 && r.StartedAtUtc >= today, ct);

            var pending = await db.Opportunities
                .Where(o => o.NextFollowUpAtUtc != null && o.NextFollowUpAtUtc <= now)
                .CountAsync(ct);
            var nextFollowUp = await db.Opportunities
                .Where(o => o.NextFollowUpAtUtc != null && o.NextFollowUpAtUtc > now)
                .OrderBy(o => o.NextFollowUpAtUtc)
                .Select(o => o.NextFollowUpAtUtc)
                .FirstOrDefaultAsync(ct);
            int? nextInDays = nextFollowUp is null ? null : Math.Max(0, (int)(nextFollowUp.Value.Date - today).TotalDays);

            return Results.Ok(new DashboardSummaryResponse(
                jobs, jobsToday, matches75, matches75Today, messages, messagesToday,
                emails, emailsToday, pending, nextInDays));
        }).WithTags("Dashboard");

        // Best opportunities: latest match per job, enriched with job + company.
        // Relevance + FRESHNESS first: stale postings (likely closed/404) are dropped, and
        // recent ones get a ranking bonus so a fresh role outranks an old high-scoring one.
        app.MapGet("/api/matches", async (
            int? minScore, int? take, int? freshDays, int? maxAgeDays,
            OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var min = minScore ?? 60;                  // relevance-first: hide weak matches
            var limit = Math.Clamp(take ?? 10, 1, 100);
            var freshSince = DateTime.UtcNow.AddDays(-(freshDays ?? 45));
            // Drop postings whose REAL publish date is older than this (default 120d): a
            // .NET role posted months ago is almost always closed -> "obsolete jobs" problem.
            var publishedSince = DateTime.UtcNow.AddDays(-(maxAgeDays ?? 120));

            var matches = await db.OpportunityMatches.Where(m => m.OverallScore >= min).ToListAsync(ct);
            var latestByJob = matches
                .GroupBy(m => m.JobPostingId)
                .Select(g => g.OrderByDescending(m => m.CreatedAtUtc).First())
                .ToList();
            if (latestByJob.Count == 0) return Results.Ok(Array.Empty<BestOpportunityResponse>());

            var jobIds = latestByJob.Select(m => m.JobPostingId).ToList();
            var jobs = await db.JobPostings.Where(j => jobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, ct);

            // Freshness bonus: very recent postings rise above older high-scoring ones,
            // while relevance (score) stays the primary signal within the fresh window.
            static double FreshnessBonus(DateTime effectiveUtc)
            {
                var ageDays = (DateTime.UtcNow - effectiveUtc).TotalDays;
                return ageDays <= 7 ? 15 : ageDays <= 30 ? 10 : ageDays <= 60 ? 5 : 0;
            }

            // Active + recent only: no expired/archived/talent-pool, discovered recently,
            // and published within the freshness window (the real "stop showing obsolete" gate).
            var ranked = latestByJob
                .Where(m => jobs.TryGetValue(m.JobPostingId, out var j)
                    && j.Status != JobPostingStatus.Expired && j.Status != JobPostingStatus.Archived
                    && !j.IsTalentPool && j.CreatedAtUtc >= freshSince
                    && j.EffectiveDateUtc >= publishedSince)
                .OrderByDescending(m => m.OverallScore + FreshnessBonus(jobs[m.JobPostingId].EffectiveDateUtc))
                .ThenByDescending(m => jobs[m.JobPostingId].EffectiveDateUtc)
                .Take(limit)
                .ToList();
            if (ranked.Count == 0) return Results.Ok(Array.Empty<BestOpportunityResponse>());

            var companyIds = ranked.Select(m => jobs[m.JobPostingId].CompanyId).Distinct().ToList();
            var companies = await db.Companies.Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

            var result = ranked.Select(m =>
            {
                var job = jobs[m.JobPostingId];
                companies.TryGetValue(job.CompanyId, out var c);
                return new BestOpportunityResponse(
                    m.Id, job.Id, job.Title, c?.Name ?? "(empresa)", job.ExtractedSkills.Take(4).ToList(),
                    m.OverallScore, m.Recommendation.ToString(), job.AbsoluteUrl, c?.WebsiteUrl,
                    job.EffectiveDateUtc, m.Rationale);
            });
            return Results.Ok(result);
        }).WithTags("Matches");

        // Recent execution runs (audit feed).
        app.MapGet("/api/runs", async (int? take, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var limit = Math.Clamp(take ?? 10, 1, 100);
            var runs = await db.ExecutionRuns
                .OrderByDescending(r => r.StartedAtUtc)
                .Take(limit)
                .ToListAsync(ct);
            return Results.Ok(runs.Select(r => new ExecutionRunResponse(
                r.Id, r.RunType, r.Status.ToString(), r.StartedAtUtc,
                r.ItemsProcessed, r.ItemsSucceeded, r.ItemsFailed)));
        }).WithTags("Runs");

        // Generated messages (drafts).
        app.MapGet("/api/messages", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var msgs = await db.GeneratedMessages
                .OrderByDescending(m => m.CreatedAtUtc)
                .Take(200)
                .ToListAsync(ct);
            return Results.Ok(msgs.Select(m => new GeneratedMessageSummary(
                m.Id, m.JobPostingId, m.EmailSubject, m.Status.ToString(), m.CreatedAtUtc)));
        }).WithTags("Messages");
    }
}
