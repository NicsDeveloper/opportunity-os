using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfFirehoseStore : IFirehoseStore
{
    private readonly OpportunityOsDbContext _db;

    public EfFirehoseStore(OpportunityOsDbContext db) => _db = db;

    public async Task AddCampaignAsync(SearchCampaign campaign, CancellationToken ct) =>
        await _db.SearchCampaigns.AddAsync(campaign, ct);

    public Task<SearchCampaign?> GetCampaignAsync(Guid id, CancellationToken ct) =>
        _db.SearchCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<SearchCampaign>> GetCampaignsAsync(CancellationToken ct) =>
        await _db.SearchCampaigns.OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);

    public Task<bool> RawCandidateExistsByUrlAsync(string url, CancellationToken ct) =>
        _db.RawJobCandidates.AnyAsync(c => c.DiscoveredUrl == url, ct);

    public Task<bool> RawCandidateExistsByFingerprintAsync(string fingerprint, CancellationToken ct) =>
        _db.RawJobCandidates.AnyAsync(c => c.NormalizedFingerprint == fingerprint, ct);

    public async Task AddRawCandidateAsync(RawJobCandidate candidate, CancellationToken ct) =>
        await _db.RawJobCandidates.AddAsync(candidate, ct);

    public async Task AddQueryExecutionAsync(SearchQueryExecution execution, CancellationToken ct) =>
        await _db.SearchQueryExecutions.AddAsync(execution, ct);

    public async Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct) =>
        await _db.ExecutionRuns.AddAsync(run, ct);

    public async Task<IReadOnlyList<RawJobCandidate>> GetRawCandidatesAsync(int take, CancellationToken ct) =>
        await _db.RawJobCandidates.OrderByDescending(c => c.DiscoveredAtUtc).Take(take).ToListAsync(ct);

    public Task<RawJobCandidate?> GetRawCandidateAsync(Guid id, CancellationToken ct) =>
        _db.RawJobCandidates.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<RawJobCandidate>> GetPromotableAsync(int minSourceConfidence, bool requireRealCompany, int take, CancellationToken ct)
    {
        var q = _db.RawJobCandidates.Where(c =>
            (c.Status == RawJobCandidateStatus.Discovered || c.Status == RawJobCandidateStatus.Classified)
            && c.SourceConfidenceScore >= minSourceConfidence);
        if (requireRealCompany) q = q.Where(c => c.RealCompanyName != null && c.RealCompanyName != "");
        return await q.OrderByDescending(c => c.SourceConfidenceScore).ThenByDescending(c => c.DiscoveredAtUtc)
            .Take(take).ToListAsync(ct);
    }

    public Task<JobPosting?> FindJobByFingerprintAsync(string fingerprint, CancellationToken ct) =>
        _db.JobPostings.FirstOrDefaultAsync(j => j.NormalizedFingerprint == fingerprint, ct);

    public async Task AddSourceOccurrenceAsync(JobPostingSourceOccurrence occurrence, CancellationToken ct) =>
        await _db.JobPostingSourceOccurrences.AddAsync(occurrence, ct);

    public Task<int> CountRawCandidatesAsync(DateTime? sinceUtc, CancellationToken ct) =>
        sinceUtc is { } since
            ? _db.RawJobCandidates.CountAsync(c => c.DiscoveredAtUtc >= since, ct)
            : _db.RawJobCandidates.CountAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
