using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// Explicit user signal about a job/candidate (relevant, irrelevant, bad company, duplicate,
/// applied...). Persisted for metrics now; can adjust ranking in a later phase.
/// </summary>
public sealed class UserFeedback
{
    public Guid Id { get; private set; }
    public Guid? JobPostingId { get; private set; }
    public Guid? RawJobCandidateId { get; private set; }
    public UserFeedbackType Type { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private UserFeedback() { }

    public UserFeedback(UserFeedbackType type, Guid? jobPostingId, Guid? rawJobCandidateId, string? reason)
    {
        Id = Guid.NewGuid();
        Type = type;
        JobPostingId = jobPostingId;
        RawJobCandidateId = rawJobCandidateId;
        Reason = reason;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
