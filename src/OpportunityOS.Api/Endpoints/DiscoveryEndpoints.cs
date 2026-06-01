using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>Firehose — massive candidate discovery (campaigns, quick/aggressive search).</summary>
public static class DiscoveryEndpoints
{
    public static void MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/discovery").WithTags("Discovery (Firehose)");

        group.MapPost("/campaigns", async (CreateCampaignRequest req, IFirehoseService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("Name is required.");
            var created = await svc.CreateCampaignAsync(req, ct);
            return Results.Created($"/api/discovery/campaigns/{created.Id}", created);
        });

        group.MapGet("/campaigns", async (IFirehoseService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCampaignsAsync(ct)));

        group.MapGet("/campaigns/{id:guid}", async (Guid id, IFirehoseService svc, CancellationToken ct) =>
        {
            var c = await svc.GetCampaignAsync(id, ct);
            return c is null ? Results.NotFound() : Results.Ok(c);
        });

        group.MapPost("/campaigns/{id:guid}/run", async (
            Guid id, AggressiveSearchRequest? req, IFirehoseService svc, CancellationToken ct) =>
        {
            var payload = (req ?? new AggressiveSearchRequest(null, null, null, null, null)) with { CampaignId = id };
            try { return Results.Ok(await svc.AggressiveSearchAsync(payload, ct)); }
            catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
        });

        group.MapPost("/quick-search", async (QuickSearchRequest req, IFirehoseService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Query)) return Results.BadRequest("Query is required.");
            return Results.Ok(await svc.QuickSearchAsync(req, ct));
        });

        group.MapPost("/aggressive-search", async (AggressiveSearchRequest req, IFirehoseService svc, CancellationToken ct) =>
        {
            try { return Results.Ok(await svc.AggressiveSearchAsync(req, ct)); }
            catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
        });

        // Raw volume (so the user can see everything the Firehose collected).
        group.MapGet("/raw-candidates", async (int? take, IFirehoseService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRawCandidatesAsync(take ?? 100, ct)));

        // Promotion: RawJobCandidate -> JobPosting (+ source occurrence, dedup by fingerprint).
        group.MapPost("/raw-candidates/{id:guid}/promote", async (
            Guid id, IRawCandidatePromotionService promo, CancellationToken ct) =>
        {
            var r = await promo.PromoteAsync(id, ct);
            return r.Promoted ? Results.Ok(r) : Results.UnprocessableEntity(r);
        });

        group.MapPost("/promote-batch", async (
            PromoteBatchRequest? req, IRawCandidatePromotionService promo, CancellationToken ct) =>
            Results.Ok(await promo.PromoteBatchAsync(req ?? new PromoteBatchRequest(null, null, null), ct)));

        // P16 — volume metrics.
        group.MapGet("/metrics", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var today = DateTime.UtcNow.Date;
            var weekAgo = today.AddDays(-7);

            var rawToday = await db.RawJobCandidates.CountAsync(c => c.DiscoveredAtUtc >= today, ct);
            var rawWeek = await db.RawJobCandidates.CountAsync(c => c.DiscoveredAtUtc >= weekAgo, ct);
            var queriesToday = await db.SearchQueryExecutions.CountAsync(e => e.StartedAtUtc >= today, ct);
            var promotedToday = await db.JobPostings.CountAsync(j => j.CreatedAtUtc >= today, ct);
            var totalRaw = await db.RawJobCandidates.CountAsync(ct);
            var dupRaw = await db.RawJobCandidates.CountAsync(c => c.Status == RawJobCandidateStatus.Duplicate, ct);
            var weak = await db.RawJobCandidates.CountAsync(c => c.SourceConfidenceScore < 40, ct);
            var avgConf = totalRaw > 0 ? (int)Math.Round(await db.RawJobCandidates.AverageAsync(c => (double)c.SourceConfidenceScore, ct)) : 0;
            var anyMatch = await db.OpportunityMatches.AnyAsync(ct);
            var avgFit = anyMatch ? (int)Math.Round(await db.OpportunityMatches.AverageAsync(m => (double)m.OverallScore, ct)) : 0;
            var actionable = await db.OpportunityMatches.Where(m => m.OverallScore >= 75).Select(m => m.JobPostingId).Distinct().CountAsync(ct);
            var relevant = await db.UserFeedbacks.CountAsync(f => f.Type == UserFeedbackType.Relevant, ct);
            var irrelevant = await db.UserFeedbacks.CountAsync(f => f.Type == UserFeedbackType.Irrelevant, ct);
            var byType = await db.RawJobCandidates.GroupBy(c => c.SourceType)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

            return Results.Ok(new DiscoveryMetricsResponse(
                rawToday, rawWeek, queriesToday, promotedToday,
                totalRaw > 0 ? Math.Round((double)dupRaw / totalRaw, 3) : 0,
                avgConf, avgFit, actionable, weak, relevant, irrelevant,
                byType.ToDictionary(x => x.Key.ToString(), x => x.Count)));
        });

        // P12 — provider quality dashboard.
        group.MapGet("/provider-quality", async (OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var execs = await db.SearchQueryExecutions.GroupBy(e => e.Provider)
                .Select(g => new { Provider = g.Key, Queries = g.Count() }).ToListAsync(ct);
            var raws = await db.RawJobCandidates.GroupBy(c => c.SourceProvider)
                .Select(g => new
                {
                    Provider = g.Key,
                    Raw = g.Count(),
                    Promoted = g.Count(x => x.Status == RawJobCandidateStatus.PromotedToJobPosting),
                    Dup = g.Count(x => x.Status == RawJobCandidateStatus.Duplicate),
                    AvgConf = g.Average(x => (double)x.SourceConfidenceScore),
                }).ToListAsync(ct);

            var providers = execs.Select(e => e.Provider).Union(raws.Select(r => r.Provider)).Distinct();
            var result = providers.Select(p =>
            {
                var e = execs.FirstOrDefault(x => x.Provider == p);
                var r = raws.FirstOrDefault(x => x.Provider == p);
                var raw = r?.Raw ?? 0;
                return new ProviderQualityResponse(
                    p, e?.Queries ?? 0, raw, r?.Promoted ?? 0,
                    raw > 0 ? Math.Round((double)(r!.Dup) / raw, 3) : 0,
                    r is null ? 0 : (int)Math.Round(r.AvgConf));
            }).OrderByDescending(x => x.RawCandidates);
            return Results.Ok(result);
        });
    }
}
