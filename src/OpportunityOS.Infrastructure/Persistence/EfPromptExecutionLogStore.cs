using OpportunityOS.Application.AI;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class EfPromptExecutionLogStore : IPromptExecutionLogStore
{
    private readonly OpportunityOsDbContext _db;

    public EfPromptExecutionLogStore(OpportunityOsDbContext db) => _db = db;

    public async Task SaveAsync(PromptExecutionLog log, CancellationToken ct)
    {
        await _db.PromptExecutionLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }
}
