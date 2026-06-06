using OpportunityOS.Contracts;

namespace OpportunityOS.Application.Import;

/// <summary>
/// Optionally refines the deterministic draft (skills/domains/seniority/roles/preferences only).
/// Default impl is a pass-through; an LLM-backed impl can be enabled by feature flag. By contract,
/// a normalizer must NEVER invent or alter experiences/education/companies/titles/dates.
/// </summary>
public interface IProfileImportNormalizer
{
    Task<CandidateProfileDraftDto> NormalizeAsync(
        LinkedInProfileImportDto parsed, CandidateProfileDraftDto deterministicDraft, CancellationToken cancellationToken);
}

/// <summary>Default: returns the deterministic draft unchanged (no LLM).</summary>
public sealed class PassthroughProfileImportNormalizer : IProfileImportNormalizer
{
    public Task<CandidateProfileDraftDto> NormalizeAsync(
        LinkedInProfileImportDto parsed, CandidateProfileDraftDto deterministicDraft, CancellationToken cancellationToken) =>
        Task.FromResult(deterministicDraft);
}
