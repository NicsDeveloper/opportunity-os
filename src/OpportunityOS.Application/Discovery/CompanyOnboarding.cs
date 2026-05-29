using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

public sealed record WebsiteDiscoveryResult(bool Found, string? WebsiteUrl);

/// <summary>Finds a company's official website from its name (heuristic / search).</summary>
public interface ICompanyWebsiteDiscoverer
{
    Task<WebsiteDiscoveryResult> DiscoverAsync(string companyName, CancellationToken ct);
}

public sealed record OnboardingResult(
    Guid ExecutionRunId, string Status, int Processed, int BoardsFound, int Errors);

public interface ICompanyOnboardingService
{
    /// <summary>For companies missing a careers/ATS board (highest priority first), find
    /// the board (CSE-ATS or heuristic) and store it — chaining the funnel toward discovery.</summary>
    Task<OnboardingResult> OnboardAsync(int limit, CancellationToken ct);
}

public sealed class CompanyOnboardingService : ICompanyOnboardingService
{
    private readonly IDiscoveryStore _store;
    private readonly IAtsBoardFinder _finder;
    private readonly ILogger<CompanyOnboardingService> _logger;

    public CompanyOnboardingService(
        IDiscoveryStore store, IAtsBoardFinder finder, ILogger<CompanyOnboardingService> logger)
    {
        _store = store;
        _finder = finder;
        _logger = logger;
    }

    public async Task<OnboardingResult> OnboardAsync(int limit, CancellationToken ct)
    {
        var run = ExecutionRun.Start("CompanyOnboarding");
        await _store.AddExecutionRunAsync(run, ct);

        var targets = (await _store.GetCompaniesByPriorityAsync(ct))
            .Where(c => string.IsNullOrWhiteSpace(c.CareersUrl))
            .Take(limit)
            .ToList();

        var boardsFound = 0;
        foreach (var company in targets)
        {
            try
            {
                var result = await _finder.FindAsync(company, ct);
                if (result.Detected && !string.IsNullOrWhiteSpace(result.BoardUrl))
                {
                    company.SetCareersUrl(result.BoardUrl!);
                    if (result.Ats is not null) company.AddTag(result.Ats.ToLowerInvariant());
                    boardsFound++;
                }
                run.RecordSuccess();
            }
            catch (Exception ex)
            {
                run.RecordFailure($"{company.Name}: {ex.Message}");
                _logger.LogError(ex, "Onboarding failed for {Company}", company.Name);
            }
        }

        await _store.SaveChangesAsync(ct);
        run.Complete();
        await _store.SaveChangesAsync(ct);

        _logger.LogInformation("CompanyOnboarding: processed={Processed} boards={Boards}", targets.Count, boardsFound);
        return new OnboardingResult(run.Id, run.Status.ToString(), targets.Count, boardsFound, run.ItemsFailed);
    }
}
