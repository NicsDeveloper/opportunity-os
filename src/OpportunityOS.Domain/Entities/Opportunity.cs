using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// An opportunity in the personal pipeline. The system may only ADVANCE it
/// automatically up to <see cref="OpportunityStatus.ReadyForHumanReview"/>;
/// anything beyond that (e.g. SentManually) requires explicit human action.
/// </summary>
public sealed class Opportunity
{
    /// <summary>Highest status the system is allowed to set without a human.</summary>
    public const OpportunityStatus MaxAutomaticStatus = OpportunityStatus.ReadyForHumanReview;

    public Guid Id { get; private set; }
    public Guid JobPostingId { get; private set; }
    public Guid? RecruiterLeadId { get; private set; }
    public OpportunityStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastActionAtUtc { get; private set; }
    public DateTime? NextFollowUpAtUtc { get; private set; }
    public string? Notes { get; private set; }

    private Opportunity() { }

    public static Opportunity Create(Guid jobPostingId, OpportunityStatus initial = OpportunityStatus.Analyzed)
    {
        EnsureAutomatic(initial);
        return new Opportunity
        {
            Id = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            Status = initial,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>System-driven transition. Throws if it would pass the human gate.</summary>
    public void AdvanceTo(OpportunityStatus status)
    {
        EnsureAutomatic(status);
        if (status > Status) // never move backwards automatically
        {
            Status = status;
            Touch();
        }
    }

    /// <summary>Human-driven transition via the API. Any status is allowed.</summary>
    public void SetStatusManually(OpportunityStatus status)
    {
        Status = status;
        Touch();
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
        Touch();
    }

    public void SetFollowUp(DateTime? nextFollowUpAtUtc)
    {
        NextFollowUpAtUtc = nextFollowUpAtUtc;
        Touch();
    }

    public void LinkRecruiter(Guid? recruiterLeadId)
    {
        RecruiterLeadId = recruiterLeadId;
        Touch();
    }

    private void Touch() => LastActionAtUtc = DateTime.UtcNow;

    private static void EnsureAutomatic(OpportunityStatus status)
    {
        if (status > MaxAutomaticStatus)
            throw new InvalidOperationException(
                $"The system cannot set status '{status}' automatically; it requires human action.");
    }
}
