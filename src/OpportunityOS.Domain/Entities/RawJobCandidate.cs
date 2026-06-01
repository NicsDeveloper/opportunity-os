using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A raw, unfiltered job candidate captured by the Firehose before strong filtering.
/// Kept even when noisy: the system collects broadly, then classifies and qualifies.
/// </summary>
public sealed class RawJobCandidate
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Snippet { get; private set; }
    public string DiscoveredUrl { get; private set; } = string.Empty;
    public string SourceProvider { get; private set; } = string.Empty;
    public string SourceName { get; private set; } = string.Empty;
    public SourceType SourceType { get; private set; }
    public string? RealCompanyName { get; private set; }
    public string? OriginalJobUrl { get; private set; }
    public string? Location { get; private set; }
    public string? WorkMode { get; private set; }
    public string? Language { get; private set; }
    public DateTime DiscoveredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public RawJobCandidateStatus Status { get; private set; }
    public int SourceConfidenceScore { get; private set; }
    public int? PreliminaryFitScore { get; private set; }
    public string? NormalizedFingerprint { get; private set; }
    public bool RequiresManualValidation { get; private set; }
    public JobVerificationStatus VerificationStatus { get; private set; } = JobVerificationStatus.Unverified;

    // Provenance: which campaign/query produced this candidate (nullable for quick-search).
    public Guid? SearchCampaignId { get; private set; }
    public string? Query { get; private set; }
    public Guid? PromotedJobPostingId { get; private set; }

    private RawJobCandidate() { }

    public RawJobCandidate(
        string title, string discoveredUrl, string sourceProvider, string sourceName,
        SourceType sourceType, int sourceConfidenceScore, bool requiresManualValidation,
        string? snippet = null, string? realCompanyName = null, string? location = null,
        string? workMode = null, string? language = null, DateTime? publishedAtUtc = null,
        Guid? searchCampaignId = null, string? query = null)
    {
        Id = Guid.NewGuid();
        Title = (title ?? string.Empty).Trim();
        DiscoveredUrl = discoveredUrl.Trim();
        SourceProvider = sourceProvider;
        SourceName = sourceName;
        SourceType = sourceType;
        SourceConfidenceScore = Math.Clamp(sourceConfidenceScore, 0, 100);
        RequiresManualValidation = requiresManualValidation;
        Snippet = snippet;
        RealCompanyName = realCompanyName;
        Location = location;
        WorkMode = workMode;
        Language = language;
        PublishedAtUtc = publishedAtUtc;
        SearchCampaignId = searchCampaignId;
        Query = query;
        DiscoveredAtUtc = DateTime.UtcNow;
        Status = RawJobCandidateStatus.Discovered;
    }

    public void SetClassification(SourceType type, string sourceName, int confidence, bool requiresManualValidation, string? realCompanyName = null)
    {
        SourceType = type;
        SourceName = sourceName;
        SourceConfidenceScore = Math.Clamp(confidence, 0, 100);
        RequiresManualValidation = requiresManualValidation;
        if (!string.IsNullOrWhiteSpace(realCompanyName)) RealCompanyName = realCompanyName;
        if (Status == RawJobCandidateStatus.Discovered) Status = RawJobCandidateStatus.Classified;
    }

    public void SetFingerprint(string fingerprint) => NormalizedFingerprint = fingerprint;
    public void SetPreliminaryFit(int score) => PreliminaryFitScore = Math.Clamp(score, 0, 100);
    public void SetOriginalJobUrl(string url) => OriginalJobUrl = url;

    public void SetVerification(JobVerificationStatus status, string? originalUrl, int confidenceBoost)
    {
        VerificationStatus = status;
        if (!string.IsNullOrWhiteSpace(originalUrl)) OriginalJobUrl = originalUrl;
        if (confidenceBoost > 0) SourceConfidenceScore = Math.Clamp(SourceConfidenceScore + confidenceBoost, 0, 100);
    }
    public void SetStatus(RawJobCandidateStatus status) => Status = status;

    public void MarkPromoted(Guid jobPostingId)
    {
        PromotedJobPostingId = jobPostingId;
        Status = RawJobCandidateStatus.PromotedToJobPosting;
    }
}
