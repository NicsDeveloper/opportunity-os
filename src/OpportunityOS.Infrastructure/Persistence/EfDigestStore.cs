using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Digest;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfDigestStore : IDigestStore
{
    private const int TopN = 3; // the digest is a focused "apply today" list, not a dump

    private readonly OpportunityOsDbContext _db;

    public EfDigestStore(OpportunityOsDbContext db) => _db = db;

    public async Task<IReadOnlyList<OpportunityDigestItem>> GetDigestItemsAsync(Guid candidateProfileId, int minScore, CancellationToken ct)
    {
        // Latest match per job FOR THIS PROFILE FIRST, then the score gate (so a re-scored/gated job drops).
        var all = await _db.OpportunityMatches
            .Where(m => m.CandidateProfileId == candidateProfileId)
            .ToListAsync(ct);
        var latestPerJob = all
            .GroupBy(m => m.JobPostingId)
            .Select(g => g.OrderByDescending(m => m.CreatedAtUtc).First())
            .Where(m => m.OverallScore >= minScore)
            .ToList();
        if (latestPerJob.Count == 0) return Array.Empty<OpportunityDigestItem>();

        var jobIds = latestPerJob.Select(m => m.JobPostingId).ToList();
        var jobs = await _db.JobPostings.Where(j => jobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, ct);
        var companyIds = jobs.Values.Select(j => j.CompanyId).Distinct().ToList();
        var companies = await _db.Companies.Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        // Hidden (irrelevant/hide/applied) jobs never go into the daily e-mail — PER PROFILE.
        var hiddenTypes = new[] { UserFeedbackType.Irrelevant, UserFeedbackType.HideSimilar, UserFeedbackType.Applied, UserFeedbackType.ContactedRecruiter };
        var hidden = (await _db.UserFeedbacks.Where(f => f.JobPostingId != null && hiddenTypes.Contains(f.Type)
                && f.CandidateProfileId == candidateProfileId)
            .Select(f => f.JobPostingId!.Value).ToListAsync(ct)).ToHashSet();

        // Learned location preference (per profile): if this profile keeps declining international
        // roles, the daily e-mail must respect it too (same rule as the board).
        var declineTypes = new[] { UserFeedbackType.Irrelevant, UserFeedbackType.HideSimilar };
        var intlDislikes = (await _db.UserFeedbacks.Where(f => declineTypes.Contains(f.Type) && f.Reason != null
                && f.CandidateProfileId == candidateProfileId)
            .Select(f => f.Reason!).ToListAsync(ct))
            .Count(r => r.ToLowerInvariant() is var rl && (rl.Contains("internacional") || rl.Contains("exterior")));

        var messages = await _db.GeneratedMessages
            .Where(g => jobIds.Contains(g.JobPostingId) && g.CandidateProfileId == candidateProfileId)
            .ToListAsync(ct);
        var latestMessageByJob = messages
            .GroupBy(g => g.JobPostingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAtUtc).First());

        var publishedSince = DateTime.UtcNow.AddDays(-120);
        var seen = new HashSet<string>();
        var items = new List<OpportunityDigestItem>();
        foreach (var match in latestPerJob.OrderByDescending(m => m.OverallScore))
        {
            if (items.Count >= TopN) break;
            if (!jobs.TryGetValue(match.JobPostingId, out var job)) continue;
            if (hidden.Contains(job.Id)) continue;
            // Active + fresh only (no expired/archived/talent-pool/obsolete).
            if (job.Status is JobPostingStatus.Expired or JobPostingStatus.Archived || job.IsTalentPool
                || job.EffectiveDateUtc < publishedSince) continue;
            // Respect the learned "no international" preference and collapse cross-source duplicates.
            if (intlDislikes >= 3 && OpportunityHeuristics.IsInternational(job)) continue;
            if (!seen.Add(OpportunityHeuristics.DedupKey(job))) continue;

            companies.TryGetValue(job.CompanyId, out var c);
            var companyName = OpportunityHeuristics.BestCompany(job, c?.Name);
            latestMessageByJob.TryGetValue(job.Id, out var msg);

            items.Add(new OpportunityDigestItem(
                companyName, job.Title, job.AbsoluteUrl, match.OverallScore, match.Recommendation.ToString(),
                match.Rationale, msg?.LinkedInMessage ?? string.Empty, msg?.CoverLetter ?? string.Empty,
                msg?.CvTailoringNotes ?? string.Empty, match.Strengths.ToList(), match.Risks.ToList(),
                OutreachLanguage: job.Language));
        }
        return items;
    }

    public async Task<IReadOnlyList<DigestProfile>> GetProfilesAsync(CancellationToken ct) =>
        await _db.CandidateProfiles
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Select(p => new DigestProfile(p.Id, p.FullName, p.MinimumScoreToShow))
            .ToListAsync(ct);

    public async Task<string?> GetCandidateNameAsync(Guid candidateProfileId, CancellationToken ct) =>
        await _db.CandidateProfiles
            .Where(p => p.Id == candidateProfileId)
            .Select(p => p.FullName)
            .FirstOrDefaultAsync(ct);

    public async Task SaveExecutionRunAsync(ExecutionRun run, CancellationToken ct)
    {
        await _db.ExecutionRuns.AddAsync(run, ct);
        await _db.SaveChangesAsync(ct);
    }
}
