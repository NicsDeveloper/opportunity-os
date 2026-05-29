using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Bacen;

/// <summary>One parsed row of the Bacen Pix participants CSV.</summary>
public sealed record BacenPixParticipantDto(
    string Name,
    string? Ispb,
    string? Cnpj,
    string InstitutionType,
    bool AuthorizedByBacen,
    string? SpiParticipationType,
    string? PixParticipationType,
    string? PixParticipationMode,
    bool? PaymentInitiation,
    bool? CashoutServiceFacilitator);

/// <summary>Downloads + parses the official Bacen Pix participants CSV.</summary>
public interface IBacenPixParticipantsCsvProvider
{
    Task<IReadOnlyCollection<BacenPixParticipantDto>> GetParticipantsAsync(CancellationToken cancellationToken);
}

public sealed record BacenImportResult(
    int TotalRead, int Created, int Updated, int Skipped, List<string> Warnings);

public sealed record BacenPromotionResult(
    int TotalEligible, int CompaniesCreated, int CompaniesUpdated, int Skipped, List<string> Warnings);

public interface IBacenRadarService
{
    Task<BacenImportResult> ImportPixParticipantsAsync(CancellationToken cancellationToken);
    Task<BacenPromotionResult> PromotePixParticipantsToCompaniesAsync(CancellationToken cancellationToken);
}

/// <summary>Persistence port for the Bacen importer/promoter.</summary>
public interface IBacenStore
{
    Task<BacenInstitution?> FindByCnpjAsync(string cnpj, CancellationToken ct);
    Task<BacenInstitution?> FindByIspbAsync(string ispb, CancellationToken ct);
    Task AddInstitutionAsync(BacenInstitution institution, CancellationToken ct);
    Task<IReadOnlyList<BacenInstitution>> GetAllInstitutionsAsync(CancellationToken ct);

    Task<Company?> FindCompanyByNameAsync(string name, CancellationToken ct);
    Task AddCompanyAsync(Company company, CancellationToken ct);

    Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
