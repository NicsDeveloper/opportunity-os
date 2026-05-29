using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Pipeline;

public interface IOpportunityPipeline
{
    /// <summary>
    /// Create an Opportunity for the match's job when the score qualifies and none
    /// exists yet. Returns the new/existing opportunity, or null when not eligible.
    /// </summary>
    Task<Opportunity?> EnsureForMatchAsync(OpportunityMatch match, CancellationToken ct);

    /// <summary>Advance an existing opportunity to MessageGenerated (system-allowed).</summary>
    Task MarkMessageGeneratedAsync(Guid jobPostingId, CancellationToken ct);
}

public sealed class OpportunityPipeline : IOpportunityPipeline
{
    /// <summary>Auto-create an Opportunity when a match scores at least this.</summary>
    public const int AutoCreateThreshold = 70;

    private readonly IOpportunityStore _store;
    private readonly ILogger<OpportunityPipeline> _logger;

    public OpportunityPipeline(IOpportunityStore store, ILogger<OpportunityPipeline> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<Opportunity?> EnsureForMatchAsync(OpportunityMatch match, CancellationToken ct)
    {
        if (match.OverallScore < AutoCreateThreshold)
            return null;

        var existing = await _store.FindByJobAsync(match.JobPostingId, ct);
        if (existing is not null)
            return existing;

        var opportunity = Opportunity.Create(match.JobPostingId, OpportunityStatus.Analyzed);
        await _store.AddAsync(opportunity, ct);
        await _store.SaveChangesAsync(ct);
        _logger.LogInformation(
            "OpportunityCreated {OpportunityId} for job {JobId} (score {Score})",
            opportunity.Id, match.JobPostingId, match.OverallScore);
        return opportunity;
    }

    public async Task MarkMessageGeneratedAsync(Guid jobPostingId, CancellationToken ct)
    {
        var opportunity = await _store.FindByJobAsync(jobPostingId, ct);
        if (opportunity is null) return;

        // System may advance only up to ReadyForHumanReview.
        opportunity.AdvanceTo(OpportunityStatus.ReadyForHumanReview);
        await _store.SaveChangesAsync(ct);
    }
}
