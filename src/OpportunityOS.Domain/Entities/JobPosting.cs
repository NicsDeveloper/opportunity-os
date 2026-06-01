using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A discovered and (optionally) normalized job posting belonging to a company.
/// </summary>
public sealed class JobPosting
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public string SourceProvider { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Department { get; private set; }
    public string? Location { get; private set; }
    public string? WorkMode { get; private set; }
    public string? Seniority { get; private set; }
    public string? Language { get; private set; }
    public string AbsoluteUrl { get; private set; } = string.Empty;
    public string? DescriptionHtml { get; private set; }
    public string DescriptionText { get; private set; } = string.Empty;
    public List<string> ExtractedSkills { get; private set; } = new();
    public List<string> ExtractedDomains { get; private set; } = new();
    public JobPostingStatus Status { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? SourceUpdatedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // ---- Source quality (Priority 4/5) ----
    public int SourceConfidenceScore { get; private set; }
    public SourceType SourceType { get; private set; } = SourceType.Unknown;
    public bool RequiresManualValidation { get; private set; }
    public string? SourceName { get; private set; }
    public string? RealCompanyName { get; private set; }
    public string? OriginalJobUrl { get; private set; }
    public string? NormalizedFingerprint { get; private set; }

    private JobPosting() { }

    public JobPosting(
        Guid companyId,
        string externalId,
        string sourceProvider,
        string title,
        string absoluteUrl,
        string descriptionText,
        string? descriptionHtml = null,
        string? department = null,
        string? location = null,
        string? language = null,
        DateTime? publishedAtUtc = null,
        DateTime? sourceUpdatedAtUtc = null)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        ExternalId = externalId;
        SourceProvider = sourceProvider;
        Title = title;
        AbsoluteUrl = absoluteUrl;
        DescriptionText = descriptionText;
        DescriptionHtml = descriptionHtml;
        Department = department;
        Location = location;
        Language = language;
        PublishedAtUtc = publishedAtUtc;
        SourceUpdatedAtUtc = sourceUpdatedAtUtc;
        Status = JobPostingStatus.Discovered;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>
    /// Refresh mutable fields when the same posting is rediscovered. Used by
    /// the dedup path (SourceProvider + ExternalId) so we never create copies.
    /// </summary>
    public void RefreshFromSource(
        string title,
        string absoluteUrl,
        string descriptionText,
        string? descriptionHtml,
        string? department,
        string? location,
        string? language,
        DateTime? sourceUpdatedAtUtc)
    {
        Title = title;
        AbsoluteUrl = absoluteUrl;
        DescriptionText = descriptionText;
        DescriptionHtml = descriptionHtml;
        Department = department;
        Location = location;
        Language = language;
        SourceUpdatedAtUtc = sourceUpdatedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyNormalization(
        string? seniority,
        string? workMode,
        string? language,
        IEnumerable<string> extractedSkills,
        IEnumerable<string> extractedDomains)
    {
        Seniority = seniority;
        WorkMode = workMode;
        Language = language ?? Language;
        ExtractedSkills = extractedSkills.ToList();
        ExtractedDomains = extractedDomains.ToList();
        if (Status == JobPostingStatus.Discovered)
            Status = JobPostingStatus.Normalized;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAnalyzed()
    {
        if (Status is JobPostingStatus.Discovered or JobPostingStatus.Normalized)
            Status = JobPostingStatus.Analyzed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = JobPostingStatus.Archived;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetFingerprint(string fingerprint) => NormalizedFingerprint = fingerprint;

    public void SetSourceQuality(
        SourceType sourceType, string? sourceName, int sourceConfidenceScore,
        bool requiresManualValidation, string? realCompanyName = null, string? originalJobUrl = null)
    {
        SourceType = sourceType;
        SourceName = sourceName;
        SourceConfidenceScore = Math.Clamp(sourceConfidenceScore, 0, 100);
        RequiresManualValidation = requiresManualValidation;
        if (!string.IsNullOrWhiteSpace(realCompanyName)) RealCompanyName = realCompanyName;
        if (!string.IsNullOrWhiteSpace(originalJobUrl)) OriginalJobUrl = originalJobUrl;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkExpired()
    {
        Status = JobPostingStatus.Expired;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Best-known posting date for freshness (published, else discovered).</summary>
    public DateTime EffectiveDateUtc => PublishedAtUtc ?? CreatedAtUtc;

    /// <summary>Talent-pool / evergreen entries (not a real, datable vacancy) — often 404.</summary>
    public bool IsTalentPool =>
        Title.Contains("banco de talentos", StringComparison.OrdinalIgnoreCase)
        || Title.Contains("talent pool", StringComparison.OrdinalIgnoreCase)
        || Title.Contains("cadastro de currículo", StringComparison.OrdinalIgnoreCase);
}
