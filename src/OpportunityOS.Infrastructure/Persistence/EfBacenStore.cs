using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Bacen;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfBacenStore : IBacenStore
{
    private readonly OpportunityOsDbContext _db;

    public EfBacenStore(OpportunityOsDbContext db) => _db = db;

    public Task<BacenInstitution?> FindByCnpjAsync(string cnpj, CancellationToken ct) =>
        _db.BacenInstitutions.FirstOrDefaultAsync(i => i.Cnpj == cnpj, ct);

    public Task<BacenInstitution?> FindByIspbAsync(string ispb, CancellationToken ct) =>
        _db.BacenInstitutions.FirstOrDefaultAsync(i => i.Ispb == ispb, ct);

    public async Task AddInstitutionAsync(BacenInstitution institution, CancellationToken ct) =>
        await _db.BacenInstitutions.AddAsync(institution, ct);

    public async Task<IReadOnlyList<BacenInstitution>> GetAllInstitutionsAsync(CancellationToken ct) =>
        await _db.BacenInstitutions.ToListAsync(ct);

    public Task<Company?> FindCompanyByNameAsync(string name, CancellationToken ct) =>
        _db.Companies.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower(), ct);

    public async Task AddCompanyAsync(Company company, CancellationToken ct) =>
        await _db.Companies.AddAsync(company, ct);

    public async Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct) =>
        await _db.ExecutionRuns.AddAsync(run, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
