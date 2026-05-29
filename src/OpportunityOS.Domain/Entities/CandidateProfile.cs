namespace OpportunityOS.Domain.Entities;

/// <summary>
/// Structured representation of the candidate's professional profile.
/// Phase 1 keeps a single active profile, but the model supports many.
/// </summary>
public sealed class CandidateProfile
{
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Headline { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string Seniority { get; private set; } = string.Empty;
    public string PreferredLanguage { get; private set; } = string.Empty;
    public List<string> CoreSkills { get; private set; } = new();
    public List<string> SecondarySkills { get; private set; } = new();
    public List<string> Domains { get; private set; } = new();
    public List<string> PreferredRoles { get; private set; } = new();
    public List<string> PreferredContractTypes { get; private set; } = new();
    public List<string> PreferredLocations { get; private set; } = new();
    public List<CandidateExperience> Experiences { get; private set; } = new();
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // Required by EF Core materialization.
    private CandidateProfile() { }

    public CandidateProfile(
        string fullName,
        string headline,
        string summary,
        string location,
        string seniority,
        string preferredLanguage,
        IEnumerable<string>? coreSkills = null,
        IEnumerable<string>? secondarySkills = null,
        IEnumerable<string>? domains = null,
        IEnumerable<string>? preferredRoles = null,
        IEnumerable<string>? preferredContractTypes = null,
        IEnumerable<string>? preferredLocations = null,
        IEnumerable<CandidateExperience>? experiences = null)
    {
        Id = Guid.NewGuid();
        FullName = fullName;
        Headline = headline;
        Summary = summary;
        Location = location;
        Seniority = seniority;
        PreferredLanguage = preferredLanguage;
        CoreSkills = coreSkills?.ToList() ?? new();
        SecondarySkills = secondarySkills?.ToList() ?? new();
        Domains = domains?.ToList() ?? new();
        PreferredRoles = preferredRoles?.ToList() ?? new();
        PreferredContractTypes = preferredContractTypes?.ToList() ?? new();
        PreferredLocations = preferredLocations?.ToList() ?? new();
        Experiences = experiences?.ToList() ?? new();
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Update(
        string fullName,
        string headline,
        string summary,
        string location,
        string seniority,
        string preferredLanguage,
        IEnumerable<string>? coreSkills,
        IEnumerable<string>? secondarySkills,
        IEnumerable<string>? domains,
        IEnumerable<string>? preferredRoles,
        IEnumerable<string>? preferredContractTypes,
        IEnumerable<string>? preferredLocations,
        IEnumerable<CandidateExperience>? experiences)
    {
        FullName = fullName;
        Headline = headline;
        Summary = summary;
        Location = location;
        Seniority = seniority;
        PreferredLanguage = preferredLanguage;
        CoreSkills = coreSkills?.ToList() ?? new();
        SecondarySkills = secondarySkills?.ToList() ?? new();
        Domains = domains?.ToList() ?? new();
        PreferredRoles = preferredRoles?.ToList() ?? new();
        PreferredContractTypes = preferredContractTypes?.ToList() ?? new();
        PreferredLocations = preferredLocations?.ToList() ?? new();
        Experiences = experiences?.ToList() ?? new();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
