using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Pipeline;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfOpportunityStore : IOpportunityStore
{
    private readonly OpportunityOsDbContext _db;

    public EfOpportunityStore(OpportunityOsDbContext db) => _db = db;

    public Task<Opportunity?> FindByJobAsync(Guid jobPostingId, Guid candidateProfileId, CancellationToken ct) =>
        _db.Opportunities.FirstOrDefaultAsync(
            o => o.JobPostingId == jobPostingId && o.CandidateProfileId == candidateProfileId, ct);

    public async Task AddAsync(Opportunity opportunity, CancellationToken ct) =>
        await _db.Opportunities.AddAsync(opportunity, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
