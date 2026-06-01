using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

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

    public async Task<Company> FindOrCreateCompanyByNameAsync(string name, CancellationToken ct)
    {
        var existing = await _db.Companies.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), ct);
        if (existing is not null) return existing;

        var company = new Company(
            name, websiteUrl: null, careersUrl: null, linkedInUrl: null,
            industry: null, country: "Brazil", CompanyPriority.Medium, CompanySource.AtsDiscovery,
            tags: new[] { "gupy", "discovered" });
        await _db.Companies.AddAsync(company, ct);
        await _db.SaveChangesAsync(ct); // persist now so later jobs resolve the same company
        return company;
    }

    public async Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct) =>
        await _db.ExecutionRuns.AddAsync(run, ct);

    public Task<CandidateProfile?> GetActiveProfileAsync(CancellationToken ct) =>
        _db.CandidateProfiles.OrderByDescending(p => p.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public Task<bool> JobHasMatchAsync(Guid jobPostingId, CancellationToken ct) =>
        _db.OpportunityMatches.AnyAsync(m => m.JobPostingId == jobPostingId, ct);

    public async Task AddMatchAsync(OpportunityMatch match, CancellationToken ct) =>
        await _db.OpportunityMatches.AddAsync(match, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
