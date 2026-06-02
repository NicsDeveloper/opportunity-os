using Hangfire;
using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Worker.Jobs;

/// <summary>
/// Optional periodic sweep of vacancies at financial institutions (Bacen-promoted companies).
/// Off by default (cost): enable with FeatureFlags:EnableBacenSweepJob. Budget-guarded.
/// </summary>
public sealed class BacenFinancialSweepJob
{
    private readonly OpportunityOsDbContext _db;
    private readonly IFirehoseService _firehose;
    private readonly IConfiguration _config;
    private readonly ILogger<BacenFinancialSweepJob> _logger;

    public BacenFinancialSweepJob(OpportunityOsDbContext db, IFirehoseService firehose, IConfiguration config, ILogger<BacenFinancialSweepJob> logger)
    {
        _db = db;
        _firehose = firehose;
        _config = config;
        _logger = logger;
    }

    [JobDisplayName("Vagas em bancos e fintechs (Bacen)")]
    [AutomaticRetry(Attempts = 1)]
    public async Task RunAsync(CancellationToken ct)
    {
        var max = _config.GetValue("Jobs:BacenSweepMaxCompanies", 50);
        var perCompany = _config.GetValue("Jobs:BacenSweepQueriesPerCompany", 8);

        var names = await _db.Companies
            .Where(c => c.Source == CompanySource.Bacen && (int)c.Priority >= (int)CompanyPriority.High
                && !c.Name.ToLower().Contains("cooperativa") && !c.Name.ToLower().Contains("sicoob")
                && !c.Name.ToLower().Contains("sicredi"))
            .OrderByDescending(c => c.Priority)
            .Take(max)
            .Select(c => c.Name)
            .ToListAsync(ct);

        if (names.Count == 0) { _logger.LogInformation("BacenFinancialSweepJob: no eligible institutions."); return; }
        var r = await _firehose.SweepCompaniesAsync("BacenFinancialSweep", names, perCompany, saveRawCandidates: true, ct);
        _logger.LogInformation("BacenFinancialSweepJob {Status}: companies={C} queries={Q} new={N}",
            r.Status, names.Count, r.QueriesExecuted, r.NewCandidates);
    }
}
