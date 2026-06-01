using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfConsultingStore : IConsultingStore
{
    private readonly OpportunityOsDbContext _db;
    public EfConsultingStore(OpportunityOsDbContext db) => _db = db;

    public Task<ConsultingCompanyCandidate?> FindCandidateByNameAsync(string name, CancellationToken ct) =>
        _db.ConsultingCompanyCandidates.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), ct);

    public async Task AddCandidateAsync(ConsultingCompanyCandidate candidate, CancellationToken ct) =>
        await _db.ConsultingCompanyCandidates.AddAsync(candidate, ct);

    public Task<ConsultingCompanyCandidate?> GetCandidateAsync(Guid id, CancellationToken ct) =>
        _db.ConsultingCompanyCandidates.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ConsultingCompanyCandidate>> GetCandidatesAsync(int take, CancellationToken ct) =>
        await _db.ConsultingCompanyCandidates
            .OrderByDescending(c => c.ConsultingConfidenceScore).ThenByDescending(c => c.CreatedAtUtc)
            .Take(take).ToListAsync(ct);

    public async Task<IReadOnlyList<ConsultingCompanyCandidate>> GetPromotableAsync(int minConfidence, int take, CancellationToken ct) =>
        await _db.ConsultingCompanyCandidates
            .Where(c => c.Status == ConsultingCompanyCandidateStatus.Candidate && c.ConsultingConfidenceScore >= minConfidence)
            .OrderByDescending(c => c.ConsultingConfidenceScore).Take(take).ToListAsync(ct);

    public Task<bool> CompanyExistsByNameAsync(string name, CancellationToken ct) =>
        _db.Companies.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct);

    public async Task AddCompanyAsync(Company company, CancellationToken ct) =>
        await _db.Companies.AddAsync(company, ct);

    public async Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct) =>
        await _db.ExecutionRuns.AddAsync(run, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
