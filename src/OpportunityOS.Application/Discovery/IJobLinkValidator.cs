namespace OpportunityOS.Application.Discovery;

public sealed record LinkValidationResult(int Checked, int Expired);

/// <summary>
/// Validates that job posting links are still live (HEAD request); marks 404/410 as
/// expired so dead postings drop out of the fresh feed. Implemented in Infrastructure.
/// </summary>
public interface IJobLinkValidator
{
    Task<LinkValidationResult> ValidateAsync(int max, CancellationToken ct);
}
