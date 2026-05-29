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
    Guid ExecutionRunId, string Status, int Processed, int WebsitesFound, int AtsDetected, int Errors);

public interface ICompanyOnboardingService
{
    /// <summary>For companies missing a website (highest priority first), discover the
    /// site then detect the ATS — chaining the funnel up to "ready to discover jobs".</summary>
    Task<OnboardingResult> OnboardAsync(int limit, CancellationToken ct);
}

public sealed class CompanyOnboardingService : ICompanyOnboardingService
{
    private readonly IDiscoveryStore _store;
    private readonly ICompanyWebsiteDiscoverer _websites;
    private readonly IAtsDetector _ats;
    private readonly ILogger<CompanyOnboardingService> _logger;

    public CompanyOnboardingService(
        IDiscoveryStore store, ICompanyWebsiteDiscoverer websites, IAtsDetector ats,
        ILogger<CompanyOnboardingService> logger)
    {
        _store = store;
        _websites = websites;
        _ats = ats;
        _logger = logger;
    }

    public async Task<OnboardingResult> OnboardAsync(int limit, CancellationToken ct)
    {
        var run = ExecutionRun.Start("CompanyOnboarding");
        await _store.AddExecutionRunAsync(run, ct);

        var targets = (await _store.GetCompaniesByPriorityAsync(ct))
            .Where(c => string.IsNullOrWhiteSpace(c.WebsiteUrl))
            .Take(limit)
            .ToList();

        int websitesFound = 0, atsDetected = 0;
        foreach (var company in targets)
        {
            try
            {
                var web = await _websites.DiscoverAsync(company.Name, ct);
                if (web is { Found: true, WebsiteUrl: { Length: > 0 } url })
                {
                    company.SetWebsiteUrl(url);
                    websitesFound++;

                    var ats = await _ats.DetectAsync(company, ct);
                    if (ats.Detected && !string.IsNullOrWhiteSpace(ats.BoardUrl))
                    {
                        company.SetCareersUrl(ats.BoardUrl!);
                        if (ats.Ats is not null) company.AddTag(ats.Ats.ToLowerInvariant());
                        atsDetected++;
                    }
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

        _logger.LogInformation("CompanyOnboarding: processed={Processed} sites={Sites} ats={Ats}",
            targets.Count, websitesFound, atsDetected);
        return new OnboardingResult(run.Id, run.Status.ToString(), targets.Count, websitesFound, atsDetected, run.ItemsFailed);
    }
}
