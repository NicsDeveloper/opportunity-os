using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Pipeline;

/// <summary>Persistence port for the opportunity pipeline.</summary>
public interface IOpportunityStore
{
    Task<Opportunity?> FindByJobAsync(Guid jobPostingId, CancellationToken ct);
    Task AddAsync(Opportunity opportunity, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
