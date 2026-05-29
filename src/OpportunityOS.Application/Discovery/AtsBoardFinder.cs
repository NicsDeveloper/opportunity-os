using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Finds a company's ATS board (careers URL). Two strategies plug in behind this:
/// a Google CSE scoped to ATS domains (name → board directly), and a heuristic
/// (discover website → crawl → detect ATS).
/// </summary>
public interface IAtsBoardFinder
{
    Task<AtsDetectionResult> FindAsync(Company company, CancellationToken ct);
}

/// <summary>
/// Heuristic board finder: if the company has no website, discover it from the
/// name; then crawl the site/careers page and detect the ATS.
/// </summary>
public sealed class HeuristicAtsBoardFinder : IAtsBoardFinder
{
    private readonly ICompanyWebsiteDiscoverer _websites;
    private readonly IAtsDetector _ats;

    public HeuristicAtsBoardFinder(ICompanyWebsiteDiscoverer websites, IAtsDetector ats)
    {
        _websites = websites;
        _ats = ats;
    }

    public async Task<AtsDetectionResult> FindAsync(Company company, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(company.WebsiteUrl) && string.IsNullOrWhiteSpace(company.CareersUrl))
        {
            var web = await _websites.DiscoverAsync(company.Name, ct);
            if (web is { Found: true, WebsiteUrl: { Length: > 0 } url })
                company.SetWebsiteUrl(url);
        }

        if (string.IsNullOrWhiteSpace(company.WebsiteUrl) && string.IsNullOrWhiteSpace(company.CareersUrl))
            return new AtsDetectionResult(false, null, null, null, null, false);

        return await _ats.DetectAsync(company, ct);
    }
}
