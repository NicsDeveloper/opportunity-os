namespace OpportunityOS.Domain.Entities;

/// <summary>
/// Structured representation of a candidate's professional profile.
/// The system supports many profiles; <see cref="IsDefault"/> marks the one used
/// when no profile is selected (back-compat anchor before auth/multi-tenancy).
/// </summary>
public sealed class CandidateProfile
{
    public Guid Id { get; private set; }
    /// <summary>
    /// The workspace (user) that owns this profile — the isolation anchor for all per-profile data.
    /// Transitionally nullable: legacy rows are backfilled by the auth seeder, and a later migration
    /// will tighten this to NOT NULL once no orphans remain.
    /// </summary>
    public Guid? WorkspaceId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    /// <summary>Short label for the profile selector (e.g. "Java Backend"). Falls back to FullName.</summary>
    public string DisplayName { get; private set; } = string.Empty;
    public string Headline { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string Seniority { get; private set; } = string.Empty;
    public string PreferredLanguage { get; private set; } = string.Empty;
    public List<string> CoreSkills { get; private set; } = new();
    public List<string> SecondarySkills { get; private set; } = new();
    /// <summary>Stacks that should DEMOTE a job (e.g. a backend dev excluding pure-frontend roles).</summary>
    public List<string> ExcludedStacks { get; private set; } = new();
    public List<string> Domains { get; private set; } = new();
    public List<string> PreferredRoles { get; private set; } = new();
    public List<string> PreferredContractTypes { get; private set; } = new();
    public List<string> PreferredLocations { get; private set; } = new();
    public List<string> PreferredWorkModes { get; private set; } = new();
    /// <summary>Minimum overall score for this profile to surface a job on its board/digest.</summary>
    public int MinimumScoreToShow { get; private set; } = 60;
    /// <summary>The fallback profile when no candidateProfileId is supplied. Exactly one should be true.</summary>
    public bool IsDefault { get; private set; }
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
        IEnumerable<CandidateExperience>? experiences = null,
        string? displayName = null,
        IEnumerable<string>? excludedStacks = null,
        IEnumerable<string>? preferredWorkModes = null,
        int minimumScoreToShow = 60,
        bool isDefault = false)
    {
        Id = Guid.NewGuid();
        FullName = fullName;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? fullName : displayName;
        Headline = headline;
        Summary = summary;
        Location = location;
        Seniority = seniority;
        PreferredLanguage = preferredLanguage;
        CoreSkills = coreSkills?.ToList() ?? new();
        SecondarySkills = secondarySkills?.ToList() ?? new();
        ExcludedStacks = excludedStacks?.ToList() ?? new();
        Domains = domains?.ToList() ?? new();
        PreferredRoles = preferredRoles?.ToList() ?? new();
        PreferredContractTypes = preferredContractTypes?.ToList() ?? new();
        PreferredLocations = preferredLocations?.ToList() ?? new();
        PreferredWorkModes = preferredWorkModes?.ToList() ?? new();
        MinimumScoreToShow = minimumScoreToShow;
        IsDefault = isDefault;
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
        IEnumerable<CandidateExperience>? experiences,
        string? displayName = null,
        IEnumerable<string>? excludedStacks = null,
        IEnumerable<string>? preferredWorkModes = null,
        int? minimumScoreToShow = null)
    {
        FullName = fullName;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? fullName : displayName;
        Headline = headline;
        Summary = summary;
        Location = location;
        Seniority = seniority;
        PreferredLanguage = preferredLanguage;
        CoreSkills = coreSkills?.ToList() ?? new();
        SecondarySkills = secondarySkills?.ToList() ?? new();
        ExcludedStacks = excludedStacks?.ToList() ?? new();
        Domains = domains?.ToList() ?? new();
        PreferredRoles = preferredRoles?.ToList() ?? new();
        PreferredContractTypes = preferredContractTypes?.ToList() ?? new();
        PreferredLocations = preferredLocations?.ToList() ?? new();
        PreferredWorkModes = preferredWorkModes?.ToList() ?? new();
        if (minimumScoreToShow is { } min) MinimumScoreToShow = min;
        Experiences = experiences?.ToList() ?? new();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Attach this profile to its owning workspace (set at creation and during the auth backfill).</summary>
    public void AssignWorkspace(Guid workspaceId)
    {
        WorkspaceId = workspaceId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Set/clear the default flag (the compatibility anchor). Caller ensures only one default.</summary>
    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
