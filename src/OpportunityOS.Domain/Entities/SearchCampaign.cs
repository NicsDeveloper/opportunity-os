using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A named, reusable massive-discovery campaign: the seed keywords, target sources and
/// budget that the Firehose expands into many queries.
/// </summary>
public sealed class SearchCampaign
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public SearchCampaignStatus Status { get; private set; }
    public SearchCampaignPriority Priority { get; private set; }
    public List<string> BaseKeywords { get; private set; } = new();
    public List<string> TargetSources { get; private set; } = new();
    public List<string> ExcludedDomains { get; private set; } = new();
    public int DailyQueryBudget { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastRunAtUtc { get; private set; }

    private SearchCampaign() { }

    public SearchCampaign(
        string name, string description, SearchCampaignPriority priority,
        IEnumerable<string> baseKeywords, IEnumerable<string>? targetSources = null,
        IEnumerable<string>? excludedDomains = null, int dailyQueryBudget = 250)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Status = SearchCampaignStatus.Active;
        Priority = priority;
        BaseKeywords = baseKeywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct().ToList() ?? new();
        TargetSources = targetSources?.ToList() ?? new();
        ExcludedDomains = excludedDomains?.ToList() ?? new();
        DailyQueryBudget = dailyQueryBudget;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkRun() => LastRunAtUtc = DateTime.UtcNow;
    public void SetStatus(SearchCampaignStatus status) => Status = status;
}
