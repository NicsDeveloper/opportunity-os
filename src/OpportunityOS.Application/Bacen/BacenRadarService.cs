using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Bacen;

/// <summary>
/// Imports the official Bacen Pix participants CSV into <see cref="BacenInstitution"/>
/// (raw radar), then promotes eligible institutions into <see cref="Company"/> with
/// criteria. Two-stage on purpose: the company radar stays curated.
/// </summary>
public sealed class BacenRadarService : IBacenRadarService
{
    private readonly IBacenPixParticipantsCsvProvider _csv;
    private readonly IBacenStore _store;
    private readonly ILogger<BacenRadarService> _logger;

    public BacenRadarService(
        IBacenPixParticipantsCsvProvider csv, IBacenStore store, ILogger<BacenRadarService> logger)
    {
        _csv = csv;
        _store = store;
        _logger = logger;
    }

    public async Task<BacenImportResult> ImportPixParticipantsAsync(CancellationToken ct)
    {
        var run = ExecutionRun.Start("BacenPixParticipantsImport");
        await _store.AddExecutionRunAsync(run, ct);
        var warnings = new List<string>();
        int created = 0, updated = 0, skipped = 0, total = 0;

        try
        {
            var participants = await _csv.GetParticipantsAsync(ct);
            total = participants.Count;
            if (total == 0)
            {
                run.Fail("Bacen CSV returned no rows.");
                await _store.SaveChangesAsync(ct);
                return new BacenImportResult(0, 0, 0, 0, new() { "CSV vazio." });
            }

            var seen = new Dictionary<string, BacenInstitution>(StringComparer.Ordinal);
            foreach (var p in participants)
            {
                var key = p.Cnpj is not null ? $"cnpj:{p.Cnpj}" : p.Ispb is not null ? $"ispb:{p.Ispb}" : null;
                if (key is null)
                {
                    skipped++;
                    warnings.Add($"Sem CNPJ nem ISPB, ignorado: {p.Name}");
                    run.RecordFailure();
                    continue;
                }

                var tags = BacenRules.GenerateTags(p);
                var existing = seen.TryGetValue(key, out var tracked)
                    ? tracked
                    : p.Cnpj is not null
                        ? await _store.FindByCnpjAsync(p.Cnpj, ct)
                        : await _store.FindByIspbAsync(p.Ispb!, ct);

                if (existing is not null)
                {
                    existing.UpdateFrom(p.Name, p.Ispb, p.Cnpj, p.InstitutionType, p.AuthorizedByBacen,
                        p.SpiParticipationType, p.PixParticipationType, p.PixParticipationMode,
                        p.PaymentInitiation, p.CashoutServiceFacilitator, tags);
                    if (tracked is null) updated++;
                    seen[key] = existing;
                }
                else
                {
                    var inst = new BacenInstitution(p.Name, p.Ispb, p.Cnpj, p.InstitutionType, p.AuthorizedByBacen,
                        p.SpiParticipationType, p.PixParticipationType, p.PixParticipationMode,
                        p.PaymentInitiation, p.CashoutServiceFacilitator, tags);
                    await _store.AddInstitutionAsync(inst, ct);
                    created++;
                    seen[key] = inst;
                }
                run.RecordSuccess();
            }

            await _store.SaveChangesAsync(ct);
            run.Complete();
            await _store.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BacenImport failed");
            run.Fail(ex.Message);
            await _store.SaveChangesAsync(ct);
            return new BacenImportResult(total, created, updated, skipped, new() { ex.Message });
        }

        _logger.LogInformation("BacenImport: read={Total} created={Created} updated={Updated} skipped={Skipped}",
            total, created, updated, skipped);
        return new BacenImportResult(total, created, updated, skipped, warnings);
    }

    public async Task<BacenPromotionResult> PromotePixParticipantsToCompaniesAsync(CancellationToken ct)
    {
        var run = ExecutionRun.Start("BacenPixParticipantsPromotion");
        await _store.AddExecutionRunAsync(run, ct);
        var warnings = new List<string>();
        int createdCompanies = 0, updatedCompanies = 0;

        var institutions = await _store.GetAllInstitutionsAsync(ct);
        var eligible = institutions.Where(i => BacenRules.IsEligibleForPromotion(BacenRules.ToDto(i))).ToList();
        var skipped = institutions.Count - eligible.Count;

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var inst in eligible)
        {
            if (!seenNames.Add(inst.Name)) continue;

            var dto = BacenRules.ToDto(inst);
            var priority = BacenRules.SuggestPriority(dto);
            var tags = inst.Tags.Append("company-radar").Distinct().ToList();

            var company = await _store.FindCompanyByNameAsync(inst.Name, ct);
            if (company is not null)
            {
                // Keep any already-discovered site/careers; just refresh priority + tags.
                company.Update(inst.Name, company.WebsiteUrl, company.CareersUrl, company.LinkedInUrl,
                    company.Industry, "Brazil", priority, tags);
                updatedCompanies++;
            }
            else
            {
                await _store.AddCompanyAsync(new Company(
                    inst.Name, websiteUrl: null, careersUrl: null, linkedInUrl: null,
                    industry: null, country: "Brazil", priority, CompanySource.Bacen, tags), ct);
                createdCompanies++;
            }
            run.RecordSuccess();
        }

        await _store.SaveChangesAsync(ct);
        run.Complete();
        await _store.SaveChangesAsync(ct);

        _logger.LogInformation("BacenPromotion: eligible={Eligible} created={Created} updated={Updated} skipped={Skipped}",
            eligible.Count, createdCompanies, updatedCompanies, skipped);
        return new BacenPromotionResult(eligible.Count, createdCompanies, updatedCompanies, skipped, warnings);
    }
}
