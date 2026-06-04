using OpportunityOS.Application.Pipeline;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.UnitTests.Fakes;

public sealed class FakeOpportunityStore : IOpportunityStore
{
    public List<Opportunity> Opportunities { get; } = new();

    public Task<Opportunity?> FindByJobAsync(Guid jobPostingId, Guid candidateProfileId, CancellationToken ct) =>
        Task.FromResult(Opportunities.FirstOrDefault(
            o => o.JobPostingId == jobPostingId && o.CandidateProfileId == candidateProfileId));

    public Task AddAsync(Opportunity opportunity, CancellationToken ct)
    {
        Opportunities.Add(opportunity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
