using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class DashboardEndpoints
{
    private static readonly string[] BrSignals =
        { "brasil", "brazil", "latam", "remoto", "são paulo", "sao paulo", "rio de janeiro",
          "belo horizonte", "curitiba", "porto alegre", "florianópolis", "florianopolis",
          "recife", "campinas", "brasília", "brasilia", "clt", "pj", "pessoa jurídica" };
    private static readonly string[] IntlSignals =
        { "united states", " usa", "u.s.", "united kingdom", " uk ", "newcastle", "london",
          "europe", "european", "canada", "india", "poland", "germany", "spain", "ireland",
          "amsterdam", "berlin", "lisbon", "portugal", "est time zone", "cet ", "gmt", "anywhere" };

    /// <summary>Heuristic: BR signal -> national; else an explicit foreign signal -> international.</summary>
    private static bool IsInternational(JobPosting j)
    {
        var t = $"{j.Title} {j.Location} {j.DescriptionText}".ToLowerInvariant();
        if (BrSignals.Any(b => t.Contains(b))) return false;
        return IntlSignals.Any(x => t.Contains(x));
    }

    // Exactly one bucket per job: "clt" | "pj" | "both" | "unknown". We only claim CLT/PJ when the
    // posting actually says so — otherwise it's "unknown" (so the PJ/CLT filters are precise, not noisy).
    private static string ContractKind(JobPosting j)
    {
        var t = $"{j.Title} {j.DescriptionText}";
        var lower = t.ToLowerInvariant();
        var rx = System.Text.RegularExpressions.RegexOptions.IgnoreCase;

        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\bclt\s*(?:ou|/|e|,|\\|x)\s*pj\b|\bpj\s*(?:ou|/|e|,|\\|x)\s*clt\b", rx))
            return "both";

        var clt = System.Text.RegularExpressions.Regex.IsMatch(t, @"\bCLT\b", rx)
            || lower.Contains("carteira assinada") || lower.Contains("regime clt") || lower.Contains("efetivo (clt)");
        var pj = System.Text.RegularExpressions.Regex.IsMatch(t, @"\bPJ\b", rx)
            || lower.Contains("pessoa jur") || lower.Contains("contractor") || lower.Contains("prestador de servi");

        if (clt && pj) return "both";
        if (clt) return "clt";
        if (pj) return "pj";
        return "unknown";
    }

    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // Aggregate counts + "today" deltas for the dashboard cards.
        app.MapGet("/api/dashboard/summary", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var today = DateTime.UtcNow.Date;
            var now = DateTime.UtcNow;

            var jobs = await db.JobPostings.CountAsync(ct);
            var jobsToday = await db.JobPostings.CountAsync(j => j.CreatedAtUtc >= today, ct);

            // "Fortes (75+)" must mean the same as the feed: strong AND fresh/active — not
            // stale/closed postings. Load strong matches + their jobs and filter in memory
            // (EffectiveDateUtc / IsTalentPool are computed, not SQL-mappable).
            var publishedSince = DateTime.UtcNow.AddDays(-120);
            var strong = await db.OpportunityMatches.Where(m => m.OverallScore >= 75).ToListAsync(ct);
            var strongLatest = strong
                .GroupBy(m => m.JobPostingId)
                .Select(g => g.OrderByDescending(m => m.CreatedAtUtc).First())
                .ToList();
            var strongJobIds = strongLatest.Select(m => m.JobPostingId).ToList();
            var strongJobs = await db.JobPostings
                .Where(j => strongJobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, ct);
            bool FreshActive(JobPosting j) =>
                j.Status != JobPostingStatus.Expired && j.Status != JobPostingStatus.Archived
                && !j.IsTalentPool && j.EffectiveDateUtc >= publishedSince;

            var matches75 = strongLatest.Count(m => strongJobs.TryGetValue(m.JobPostingId, out var j) && FreshActive(j));
            var matches75Today = strongLatest.Count(m =>
                m.CreatedAtUtc >= today && strongJobs.TryGetValue(m.JobPostingId, out var j) && FreshActive(j));

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
            int? minScore, int? take, int? freshDays, int? maxAgeDays, string? sort,
            string? region, string? contract,
            OpportunityOsDbContext db, IDiscoveryRankService ranker, IFeedbackLearningService learner, CancellationToken ct) =>
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

            // Active + recent only: no expired/archived/talent-pool, discovered recently,
            // and published within the freshness window (the real "stop showing obsolete" gate).
            var filtered = latestByJob
                .Where(m => jobs.TryGetValue(m.JobPostingId, out var j)
                    && j.Status != JobPostingStatus.Expired && j.Status != JobPostingStatus.Archived
                    && !j.IsTalentPool && j.CreatedAtUtc >= freshSince
                    && j.EffectiveDateUtc >= publishedSince)
                .ToList();
            if (filtered.Count == 0) return Results.Ok(Array.Empty<BestOpportunityResponse>());

            // User feedback (B7): hide jobs marked irrelevant/ocultar; boost/penalize the rest.
            var feedback = await db.UserFeedbacks
                .Where(f => f.JobPostingId != null && jobIds.Contains(f.JobPostingId!.Value))
                .ToListAsync(ct);
            var fbByJob = feedback.GroupBy(f => f.JobPostingId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(f => f.Type).ToList());
            // Hidden from the main board: irrelevant/hide-similar AND "já me cadastrei/apliquei"
            // (Applied/ContactedRecruiter) — those move to the Applications board so the user can
            // keep killing opportunities they've already acted on.
            var hidden = fbByJob
                .Where(kv => kv.Value.Any(t => t is UserFeedbackType.Irrelevant or UserFeedbackType.HideSimilar
                    or UserFeedbackType.Applied or UserFeedbackType.ContactedRecruiter))
                .Select(kv => kv.Key).ToHashSet();
            filtered = filtered.Where(m => !hidden.Contains(m.JobPostingId)).ToList();

            // Continuous improvement: learn from EVERYTHING the user rejected (not only the current
            // candidates) so jobs similar to past "irrelevante"/"ocultar" — same company, stack, or the
            // words the user typed as the reason — get demoted and, when the signal is strong, hidden.
            var negTypes = new[] { UserFeedbackType.Irrelevant, UserFeedbackType.HideSimilar };
            var negFb = await db.UserFeedbacks
                .Where(f => f.JobPostingId != null && negTypes.Contains(f.Type))
                .Select(f => new { JobId = f.JobPostingId!.Value, f.Reason })
                .ToListAsync(ct);
            var negModel = NegativeModel.Empty;
            if (negFb.Count > 0)
            {
                var negJobIds = negFb.Select(f => f.JobId).Distinct().ToList();
                var negJobs = await db.JobPostings.Where(j => negJobIds.Contains(j.Id))
                    .Select(j => new { j.Id, j.CompanyId, j.ExtractedSkills, j.Title })
                    .ToDictionaryAsync(j => j.Id, ct);
                var disliked = negFb.Where(f => negJobs.ContainsKey(f.JobId))
                    .Select(f => { var nj = negJobs[f.JobId]; return new DislikedJob(nj.CompanyId, nj.ExtractedSkills, nj.Title, f.Reason); })
                    .ToList();
                negModel = learner.Build(disliked);
                filtered = filtered.Where(m =>
                {
                    var j = jobs[m.JobPostingId];
                    return !learner.ShouldHide(negModel, new JobSignal(j.CompanyId, j.ExtractedSkills, j.Title, j.DescriptionText));
                }).ToList();

                // Learned location preference: the user keeps declining international roles
                // ("Internacional" is a LOCATION, not a word in the posting, so token-learning misses it).
                // Once the signal is clear, hide international by default — still reachable via "Exterior".
                if (negModel.IntlDislikes >= 3 && !string.Equals(region, "international", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(m => !IsInternational(jobs[m.JobPostingId])).ToList();
            }

            // Region + contract filters (user can ask national/international, PJ/CLT, or both).
            if (!string.Equals(region, "all", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(region))
                filtered = filtered.Where(m => string.Equals(region, "international", StringComparison.OrdinalIgnoreCase)
                    ? IsInternational(jobs[m.JobPostingId]) : !IsInternational(jobs[m.JobPostingId])).ToList();
            // Precise contract filter: exact bucket match (clt | pj | both | unknown). "all" = no filter.
            if (!string.Equals(contract, "all", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(contract))
                filtered = filtered.Where(m =>
                    string.Equals(ContractKind(jobs[m.JobPostingId]), contract, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filtered.Count == 0) return Results.Ok(Array.Empty<BestOpportunityResponse>());

            var companyIds = filtered.Select(m => jobs[m.JobPostingId].CompanyId).Distinct().ToList();
            var companies = await db.Companies.Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

            int LearnPenalty(Domain.Entities.JobPosting job) =>
                learner.Penalty(negModel, new JobSignal(job.CompanyId, job.ExtractedSkills, job.Title, job.DescriptionText));

            int DiscoveryRankOf(Domain.Entities.OpportunityMatch m)
            {
                var job = jobs[m.JobPostingId];
                companies.TryGetValue(job.CompanyId, out var c);
                var boost = ranker.FeedbackBoost(fbByJob.TryGetValue(m.JobPostingId, out var types) ? types : Enumerable.Empty<UserFeedbackType>());
                return ranker.ComputeDiscoveryRank(new DiscoveryRankInput(
                    m.OverallScore, job.SourceConfidenceScore, job.EffectiveDateUtc, c?.Priority ?? CompanyPriority.Low, boost))
                    - LearnPenalty(job);
            }

            // Freshness bonus keeps relevance primary within the fresh window (default sort);
            // sort=rank orders by the full DiscoveryRank blend (Qualified view).
            static double FreshnessBonus(DateTime effectiveUtc)
            {
                var ageDays = (DateTime.UtcNow - effectiveUtc).TotalDays;
                return ageDays <= 7 ? 15 : ageDays <= 30 ? 10 : ageDays <= 60 ? 5 : 0;
            }

            var ordered = string.Equals(sort, "rank", StringComparison.OrdinalIgnoreCase)
                ? filtered.OrderByDescending(DiscoveryRankOf).ThenByDescending(m => jobs[m.JobPostingId].EffectiveDateUtc)
                : filtered.OrderByDescending(m => m.OverallScore + FreshnessBonus(jobs[m.JobPostingId].EffectiveDateUtc) - LearnPenalty(jobs[m.JobPostingId]))
                          .ThenByDescending(m => jobs[m.JobPostingId].EffectiveDateUtc);
            var ranked = ordered.Take(limit).ToList();

            var result = ranked.Select(m =>
            {
                var job = jobs[m.JobPostingId];
                companies.TryGetValue(job.CompanyId, out var c);
                return new BestOpportunityResponse(
                    m.Id, job.Id, job.Title, c?.Name ?? "(empresa)", job.ExtractedSkills.Take(4).ToList(),
                    m.OverallScore, m.Recommendation.ToString(), job.AbsoluteUrl, c?.WebsiteUrl,
                    job.EffectiveDateUtc, m.Rationale,
                    DiscoveryRankOf(m), job.SourceType.ToString(), job.SourceConfidenceScore,
                    job.RequiresManualValidation, job.RealCompanyName, job.SourceName, job.HasSourceDate);
            });
            return Results.Ok(result);
        }).WithTags("Matches");

        // Applications board: opportunities the user already acted on ("já me cadastrei"/apliquei
        // or contatei recrutador). These leave the main board (above) and live here so the user
        // can track what's already been handled. Ordered by when it was marked (most recent first).
        app.MapGet("/api/applications", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var appliedTypes = new[] { UserFeedbackType.Applied, UserFeedbackType.ContactedRecruiter };
            var fb = await db.UserFeedbacks
                .Where(f => f.JobPostingId != null && appliedTypes.Contains(f.Type))
                .ToListAsync(ct);
            if (fb.Count == 0) return Results.Ok(Array.Empty<ApplicationResponse>());

            // Most recent action per job (and its type, for the label).
            var byJob = fb.GroupBy(f => f.JobPostingId!.Value)
                .Select(g => g.OrderByDescending(f => f.CreatedAtUtc).First())
                .ToList();
            var jobIds = byJob.Select(f => f.JobPostingId!.Value).ToList();

            var jobs = await db.JobPostings.Where(j => jobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, ct);
            var companyIds = jobs.Values.Select(j => j.CompanyId).Distinct().ToList();
            var companies = await db.Companies.Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
            var matches = await db.OpportunityMatches.Where(m => jobIds.Contains(m.JobPostingId)).ToListAsync(ct);
            var scoreByJob = matches.GroupBy(m => m.JobPostingId)
                .ToDictionary(g => g.Key, g => g.Max(m => m.OverallScore));

            var result = byJob
                .Where(f => jobs.ContainsKey(f.JobPostingId!.Value))
                .OrderByDescending(f => f.CreatedAtUtc)
                .Select(f =>
                {
                    var job = jobs[f.JobPostingId!.Value];
                    companies.TryGetValue(job.CompanyId, out var c);
                    return new ApplicationResponse(
                        job.Id, job.Title, c?.Name ?? job.RealCompanyName ?? "(empresa)", job.AbsoluteUrl,
                        c?.WebsiteUrl, scoreByJob.GetValueOrDefault(job.Id), f.Type.ToString(),
                        f.CreatedAtUtc, job.EffectiveDateUtc);
                })
                .ToList();
            return Results.Ok(result);
        }).WithTags("Matches");

        // Undo: move an application back to the main board by removing its Applied/ContactedRecruiter feedback.
        app.MapDelete("/api/applications/{jobId:guid}", async (Guid jobId, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var appliedTypes = new[] { UserFeedbackType.Applied, UserFeedbackType.ContactedRecruiter };
            var toRemove = await db.UserFeedbacks
                .Where(f => f.JobPostingId == jobId && appliedTypes.Contains(f.Type))
                .ToListAsync(ct);
            if (toRemove.Count == 0) return Results.NotFound();
            db.UserFeedbacks.RemoveRange(toRemove);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
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
