using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Projections;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfLatestOpportunityMatchProjection : ILatestOpportunityMatchProjection
{
    private readonly OpportunityOsDbContext _db;
    private readonly ILogger<EfLatestOpportunityMatchProjection> _logger;

    public EfLatestOpportunityMatchProjection(OpportunityOsDbContext db, ILogger<EfLatestOpportunityMatchProjection> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task UpsertAsync(OpportunityMatch match, CancellationToken ct)
    {
        // Look at the unit of work first (the same request may have just upserted this pair),
        // then the database. Never read or touch another profile's row.
        var existing = _db.LatestOpportunityMatches.Local.FirstOrDefault(
                p => p.JobPostingId == match.JobPostingId && p.CandidateProfileId == match.CandidateProfileId)
            ?? await _db.LatestOpportunityMatches.FirstOrDefaultAsync(
                p => p.JobPostingId == match.JobPostingId && p.CandidateProfileId == match.CandidateProfileId, ct);

        if (existing is null)
            await _db.LatestOpportunityMatches.AddAsync(LatestOpportunityMatch.From(match), ct);
        else
            existing.UpdateFrom(match); // only moves forward in time (guarded in the entity)
    }

    public async Task<LatestMatchBackfillResult> BackfillAsync(CancellationToken ct)
    {
        // 1) Latest match id per (job, profile) — lightweight scan.
        var keys = await _db.OpportunityMatches
            .Select(m => new { m.Id, m.JobPostingId, m.CandidateProfileId, m.CreatedAtUtc })
            .ToListAsync(ct);
        var latestIds = keys
            .GroupBy(m => (m.JobPostingId, m.CandidateProfileId))
            .Select(g => g.OrderByDescending(m => m.CreatedAtUtc).First().Id)
            .ToHashSet();

        // 2) Load just those matches + the existing projection rows.
        var latestMatches = await _db.OpportunityMatches.Where(m => latestIds.Contains(m.Id)).ToListAsync(ct);
        var existing = await _db.LatestOpportunityMatches.ToListAsync(ct);
        var byPair = existing.ToDictionary(p => (p.JobPostingId, p.CandidateProfileId));

        int created = 0, updated = 0, skipped = 0;
        foreach (var match in latestMatches)
        {
            if (byPair.TryGetValue((match.JobPostingId, match.CandidateProfileId), out var projection))
            {
                if (projection.UpdateFrom(match)) updated++;
                else skipped++;
            }
            else
            {
                var projection2 = LatestOpportunityMatch.From(match);
                _db.LatestOpportunityMatches.Add(projection2);
                byPair[(match.JobPostingId, match.CandidateProfileId)] = projection2;
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);
        var result = new LatestMatchBackfillResult(latestMatches.Count, created, updated, skipped);
        _logger.LogInformation(
            "LatestOpportunityMatch backfill: pairs={Pairs} created={Created} updated={Updated} skipped={Skipped}",
            result.ProcessedPairs, result.Created, result.Updated, result.Skipped);
        return result;
    }
}
