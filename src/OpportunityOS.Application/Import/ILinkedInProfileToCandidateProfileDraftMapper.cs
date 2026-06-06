using OpportunityOS.Contracts;

namespace OpportunityOS.Application.Import;

/// <summary>Deterministic mapping of a parsed LinkedIn profile to an editable CandidateProfile draft.</summary>
public interface ILinkedInProfileToCandidateProfileDraftMapper
{
    CandidateProfileDraftDto Map(LinkedInProfileImportDto parsed);
}
