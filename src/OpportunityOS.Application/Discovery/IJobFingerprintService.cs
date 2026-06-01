namespace OpportunityOS.Application.Discovery;

public sealed record JobFingerprintInput(
    string Title,
    string? Company,
    string? Location,
    string? Seniority,
    IEnumerable<string>? TopSkills = null,
    string? DescriptionText = null);

/// <summary>
/// Produces a stable fingerprint for a posting so the same vacancy found on different
/// sources collapses to one. Based on normalized title + company + location + seniority +
/// top skills + a short description hash.
/// </summary>
public interface IJobFingerprintService
{
    string GenerateFingerprint(JobFingerprintInput input);
}
