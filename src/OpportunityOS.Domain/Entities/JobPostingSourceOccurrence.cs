using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A place where a (deduplicated) JobPosting was found. The same vacancy can surface on
/// several sources; the JobPosting stays single and each source becomes an occurrence,
/// with the best (most official) source kept as the posting's primary.
/// </summary>
public sealed class JobPostingSourceOccurrence
{
    public Guid Id { get; private set; }
    public Guid JobPostingId { get; private set; }
    public string SourceProvider { get; private set; } = string.Empty;
    public string SourceName { get; private set; } = string.Empty;
    public SourceType SourceType { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public DateTime DiscoveredAtUtc { get; private set; }
    public int SourceConfidenceScore { get; private set; }

    private JobPostingSourceOccurrence() { }

    public JobPostingSourceOccurrence(
        Guid jobPostingId, string sourceProvider, string sourceName, SourceType sourceType,
        string url, int sourceConfidenceScore)
    {
        Id = Guid.NewGuid();
        JobPostingId = jobPostingId;
        SourceProvider = sourceProvider;
        SourceName = sourceName;
        SourceType = sourceType;
        Url = url;
        SourceConfidenceScore = Math.Clamp(sourceConfidenceScore, 0, 100);
        DiscoveredAtUtc = DateTime.UtcNow;
    }
}
