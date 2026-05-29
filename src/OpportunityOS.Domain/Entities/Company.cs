using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A target company being monitored for opportunities.
/// </summary>
public sealed class Company
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? WebsiteUrl { get; private set; }
    public string? CareersUrl { get; private set; }
    public string? LinkedInUrl { get; private set; }
    public string? Industry { get; private set; }
    public string? Country { get; private set; }
    public CompanyPriority Priority { get; private set; }
    public CompanySource Source { get; private set; }
    public List<string> Tags { get; private set; } = new();
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastScannedAtUtc { get; private set; }

    private Company() { }

    public Company(
        string name,
        string? websiteUrl,
        string? careersUrl,
        string? linkedInUrl,
        string? industry,
        string? country,
        CompanyPriority priority,
        CompanySource source,
        IEnumerable<string>? tags = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        WebsiteUrl = websiteUrl;
        CareersUrl = careersUrl;
        LinkedInUrl = linkedInUrl;
        Industry = industry;
        Country = country;
        Priority = priority;
        Source = source;
        Tags = tags?.ToList() ?? new();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Update(
        string name,
        string? websiteUrl,
        string? careersUrl,
        string? linkedInUrl,
        string? industry,
        string? country,
        CompanyPriority priority,
        IEnumerable<string>? tags)
    {
        Name = name;
        WebsiteUrl = websiteUrl;
        CareersUrl = careersUrl;
        LinkedInUrl = linkedInUrl;
        Industry = industry;
        Country = country;
        Priority = priority;
        Tags = tags?.ToList() ?? new();
    }

    public void MarkScanned() => LastScannedAtUtc = DateTime.UtcNow;

    /// <summary>Set the careers/ATS board URL discovered by the ATS detector.</summary>
    public void SetCareersUrl(string careersUrl)
    {
        if (!string.IsNullOrWhiteSpace(careersUrl)) CareersUrl = careersUrl;
    }

    public void AddTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag) && !Tags.Contains(tag)) Tags.Add(tag);
    }
}
