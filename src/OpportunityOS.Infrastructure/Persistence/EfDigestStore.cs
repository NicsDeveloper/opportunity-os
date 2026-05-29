using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Digest;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfDigestStore : IDigestStore
{
    private readonly OpportunityOsDbContext _db;

    public EfDigestStore(OpportunityOsDbContext db) => _db = db;

    public async Task<IReadOnlyList<OpportunityDigestItem>> GetDigestItemsAsync(int minScore, CancellationToken ct)
    {
        var matches = await _db.OpportunityMatches
            .Where(m => m.OverallScore >= minScore)
            .ToListAsync(ct);

        // Keep the latest match per job.
        var latestPerJob = matches
            .GroupBy(m => m.JobPostingId)
            .Select(g => g.OrderByDescending(m => m.CreatedAtUtc).First())
            .ToList();
        if (latestPerJob.Count == 0)
            return Array.Empty<OpportunityDigestItem>();

        var jobIds = latestPerJob.Select(m => m.JobPostingId).ToList();
        var jobs = await _db.JobPostings.Where(j => jobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, ct);
        var companyIds = jobs.Values.Select(j => j.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var messages = await _db.GeneratedMessages.Where(g => jobIds.Contains(g.JobPostingId)).ToListAsync(ct);
        var latestMessageByJob = messages
            .GroupBy(g => g.JobPostingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAtUtc).First());

        var items = new List<OpportunityDigestItem>();
        foreach (var match in latestPerJob.OrderByDescending(m => m.OverallScore))
        {
            if (!jobs.TryGetValue(match.JobPostingId, out var job)) continue;
            var companyName = companies.TryGetValue(job.CompanyId, out var c) ? c.Name : "(empresa)";
            latestMessageByJob.TryGetValue(job.Id, out var msg);

            items.Add(new OpportunityDigestItem(
                companyName, job.Title, job.AbsoluteUrl, match.OverallScore, match.Recommendation.ToString(),
                match.Rationale, msg?.LinkedInMessage ?? string.Empty, msg?.CoverLetter ?? string.Empty,
                msg?.CvTailoringNotes ?? string.Empty, match.Strengths.ToList(), match.Risks.ToList()));
        }
        return items;
    }

    public async Task SaveExecutionRunAsync(ExecutionRun run, CancellationToken ct)
    {
        await _db.ExecutionRuns.AddAsync(run, ct);
        await _db.SaveChangesAsync(ct);
    }
}
