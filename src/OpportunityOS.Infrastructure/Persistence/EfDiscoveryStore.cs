using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfDiscoveryStore : IDiscoveryStore
{
    private readonly OpportunityOsDbContext _db;

    public EfDiscoveryStore(OpportunityOsDbContext db) => _db = db;

    public async Task<IReadOnlyList<Company>> GetCompaniesByPriorityAsync(CancellationToken ct) =>
        await _db.Companies
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.LastScannedAtUtc)
            .ToListAsync(ct);

    public Task<Company?> GetCompanyAsync(Guid companyId, CancellationToken ct) =>
        _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct);

    public Task<JobPosting?> FindJobAsync(string sourceProvider, string externalId, CancellationToken ct) =>
        _db.JobPostings.FirstOrDefaultAsync(
            j => j.SourceProvider == sourceProvider && j.ExternalId == externalId, ct);

    public async Task AddJobAsync(JobPosting job, CancellationToken ct) =>
        await _db.JobPostings.AddAsync(job, ct);

    public async Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct) =>
        await _db.ExecutionRuns.AddAsync(run, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
