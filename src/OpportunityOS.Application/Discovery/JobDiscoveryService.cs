using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

public sealed record DiscoveryResult(
    Guid ExecutionRunId,
    string Status,
    int CompaniesProcessed,
    int ProvidersInvoked,
    int JobsDiscovered,
    int JobsUpdated,
    int Errors);

public interface IJobDiscoveryService
{
    /// <summary>Discover jobs for one company (when <paramref name="companyId"/> is set) or all.</summary>
    Task<DiscoveryResult> DiscoverAsync(Guid? companyId, CancellationToken ct);

    /// <summary>Discover jobs by keyword across search providers (e.g. Gupy); auto-creates companies.</summary>
    Task<DiscoveryResult> SearchAsync(IReadOnlyCollection<string> keywords, CancellationToken ct);
}

/// <summary>
/// Orchestrates discovery across companies and providers. Resilient by design:
/// a single provider/company failure is logged and recorded, never aborting the run.
/// Deduplicates by (SourceProvider, ExternalId): rediscovered postings are refreshed,
/// never duplicated.
/// </summary>
public sealed class JobDiscoveryService : IJobDiscoveryService
{
    private readonly IEnumerable<IJobSourceProvider> _providers;
    private readonly IEnumerable<IJobSearchProvider> _searchProviders;
    private readonly IDiscoveryStore _store;
    private readonly ILogger<JobDiscoveryService> _logger;

    public JobDiscoveryService(
        IEnumerable<IJobSourceProvider> providers,
        IEnumerable<IJobSearchProvider> searchProviders,
        IDiscoveryStore store,
        ILogger<JobDiscoveryService> logger)
    {
        _providers = providers;
        _searchProviders = searchProviders;
        _store = store;
        _logger = logger;
    }

    public async Task<DiscoveryResult> SearchAsync(IReadOnlyCollection<string> keywords, CancellationToken ct)
    {
        var run = ExecutionRun.Start("SearchJobs");
        await _store.AddExecutionRunAsync(run, ct);

        int newJobs = 0, updatedJobs = 0, providersInvoked = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var companies = new HashSet<Guid>();

        foreach (var provider in _searchProviders)
        {
            providersInvoked++;
            try
            {
                var jobs = await provider.SearchAsync(keywords, ct);
                foreach (var dto in jobs)
                {
                    var key = $"{dto.SourceProvider}::{dto.ExternalId}";
                    if (!seen.Add(key)) continue;

                    var company = await _store.FindOrCreateCompanyByNameAsync(dto.CompanyName, ct);
                    companies.Add(company.Id);
                    if (await UpsertAsync(company.Id, dto, ct)) newJobs++;
                    else updatedJobs++;
                }
                await _store.SaveChangesAsync(ct);
                run.RecordSuccess();
            }
            catch (Exception ex)
            {
                run.RecordFailure($"{provider.ProviderName}: {ex.Message}");
                _logger.LogError(ex, "SearchProviderFailed {Provider}", provider.ProviderName);
            }
        }

        run.Complete();
        await _store.SaveChangesAsync(ct);
        return new DiscoveryResult(
            run.Id, run.Status.ToString(), companies.Count, providersInvoked, newJobs, updatedJobs, run.ItemsFailed);
    }

    public async Task<DiscoveryResult> DiscoverAsync(Guid? companyId, CancellationToken ct)
    {
        var run = ExecutionRun.Start("DiscoverJobs");
        await _store.AddExecutionRunAsync(run, ct);

        var companies = companyId is { } id
            ? await GetSingleCompany(id, ct)
            : await _store.GetCompaniesByPriorityAsync(ct);

        int newJobs = 0, updatedJobs = 0, providersInvoked = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var company in companies)
        {
            var providers = _providers.Where(p => p.CanHandle(company)).ToList();
            _logger.LogInformation("CompanyScanStarted {Company} (providers: {Count})", company.Name, providers.Count);

            foreach (var provider in providers)
            {
                providersInvoked++;
                try
                {
                    var jobs = await provider.DiscoverJobsAsync(company, ct);
                    foreach (var dto in jobs)
                    {
                        var key = $"{dto.SourceProvider}::{dto.ExternalId}";
                        if (!seen.Add(key)) continue; // ignore intra-run duplicates

                        if (await UpsertAsync(company.Id, dto, ct)) newJobs++;
                        else updatedJobs++;
                    }
                    run.RecordSuccess();
                }
                catch (Exception ex)
                {
                    run.RecordFailure($"{provider.ProviderName}/{company.Name}: {ex.Message}");
                    _logger.LogError(ex, "ProviderFailed {Provider} {Company}", provider.ProviderName, company.Name);
                }
            }

            company.MarkScanned();
            await _store.SaveChangesAsync(ct); // persist progress + make dedup queries see new rows
            _logger.LogInformation("CompanyScanCompleted {Company}", company.Name);
        }

        run.Complete();
        await _store.SaveChangesAsync(ct);

        return new DiscoveryResult(
            run.Id, run.Status.ToString(), companies.Count, providersInvoked, newJobs, updatedJobs, run.ItemsFailed);
    }

    /// <returns>true if a new posting was created; false if an existing one was refreshed.</returns>
    private async Task<bool> UpsertAsync(Guid companyId, DiscoveredJobDto dto, CancellationToken ct)
    {
        var existing = await _store.FindJobAsync(dto.SourceProvider, dto.ExternalId, ct);
        if (existing is null)
        {
            var job = new JobPosting(
                companyId, dto.ExternalId, dto.SourceProvider, dto.Title, dto.AbsoluteUrl,
                dto.DescriptionText ?? string.Empty, dto.DescriptionHtml, dto.Department,
                dto.Location, dto.Language, dto.PublishedAtUtc, dto.UpdatedAtUtc);
            await _store.AddJobAsync(job, ct);
            _logger.LogInformation("JobDiscovered {Provider} {ExternalId} {Title}", dto.SourceProvider, dto.ExternalId, dto.Title);
            return true;
        }

        existing.RefreshFromSource(
            dto.Title, dto.AbsoluteUrl, dto.DescriptionText ?? string.Empty, dto.DescriptionHtml,
            dto.Department, dto.Location, dto.Language, dto.UpdatedAtUtc);
        _logger.LogInformation("JobUpdated {Provider} {ExternalId}", dto.SourceProvider, dto.ExternalId);
        return false;
    }

    private async Task<IReadOnlyList<Company>> GetSingleCompany(Guid id, CancellationToken ct)
    {
        var company = await _store.GetCompanyAsync(id, ct);
        return company is null ? Array.Empty<Company>() : new[] { company };
    }
}
