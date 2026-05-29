namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A single professional experience. Stored as part of the candidate profile
/// (jsonb) rather than its own table to keep the Phase 1 model lean.
/// </summary>
public sealed class CandidateExperience
{
    public string Company { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public List<string> Technologies { get; set; } = new();
    public List<string> Domains { get; set; } = new();
    public List<string> Achievements { get; set; } = new();
}
