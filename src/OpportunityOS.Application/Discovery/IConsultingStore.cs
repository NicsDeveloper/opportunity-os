using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

/// <summary>Persistence port for the Consulting Radar (candidates + promotion to Company).</summary>
public interface IConsultingStore
{
    Task<ConsultingCompanyCandidate?> FindCandidateByNameAsync(string name, CancellationToken ct);
    Task AddCandidateAsync(ConsultingCompanyCandidate candidate, CancellationToken ct);
    Task<ConsultingCompanyCandidate?> GetCandidateAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ConsultingCompanyCandidate>> GetCandidatesAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<ConsultingCompanyCandidate>> GetPromotableAsync(int minConfidence, int take, CancellationToken ct);

    Task<bool> CompanyExistsByNameAsync(string name, CancellationToken ct);
    Task AddCompanyAsync(Company company, CancellationToken ct);

    Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
