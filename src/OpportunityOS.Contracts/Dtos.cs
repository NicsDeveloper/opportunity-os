namespace OpportunityOS.Contracts;

// ---- Candidate Profile ----

public sealed record CandidateExperienceDto(
    string Company,
    string Role,
    string Period,
    List<string> Technologies,
    List<string> Domains,
    List<string> Achievements);

public sealed record CandidateProfileRequest(
    string FullName,
    string Headline,
    string Summary,
    string Location,
    string Seniority,
    string PreferredLanguage,
    List<string>? CoreSkills,
    List<string>? SecondarySkills,
    List<string>? Domains,
    List<string>? PreferredRoles,
    List<string>? PreferredContractTypes,
    List<string>? PreferredLocations,
    List<CandidateExperienceDto>? Experiences);

public sealed record CandidateProfileResponse(
    Guid Id,
    string FullName,
    string Headline,
    string Summary,
    string Location,
    string Seniority,
    string PreferredLanguage,
    List<string> CoreSkills,
    List<string> SecondarySkills,
    List<string> Domains,
    List<string> PreferredRoles,
    List<string> PreferredContractTypes,
    List<string> PreferredLocations,
    List<CandidateExperienceDto> Experiences,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

// ---- Company ----

public sealed record CompanyRequest(
    string Name,
    string? WebsiteUrl,
    string? CareersUrl,
    string? LinkedInUrl,
    string? Industry,
    string? Country,
    int Priority,
    List<string>? Tags);

public sealed record CompanyResponse(
    Guid Id,
    string Name,
    string? WebsiteUrl,
    string? CareersUrl,
    string? LinkedInUrl,
    string? Industry,
    string? Country,
    string Priority,
    string Source,
    List<string> Tags,
    DateTime CreatedAtUtc,
    DateTime? LastScannedAtUtc);

// ---- Job Posting ----

public sealed record JobPostingResponse(
    Guid Id,
    Guid CompanyId,
    string ExternalId,
    string SourceProvider,
    string Title,
    string? Department,
    string? Location,
    string? WorkMode,
    string? Seniority,
    string? Language,
    string AbsoluteUrl,
    string DescriptionText,
    List<string> ExtractedSkills,
    List<string> ExtractedDomains,
    string Status,
    DateTime? PublishedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

// ---- Match ----

public sealed record MatchResponse(
    Guid Id,
    Guid JobPostingId,
    Guid CandidateProfileId,
    int OverallScore,
    int TechnicalScore,
    int DomainScore,
    int SeniorityScore,
    int LocationScore,
    int LanguageScore,
    string Recommendation,
    List<string> Strengths,
    List<string> Risks,
    List<string> MissingRequirements,
    string Rationale,
    DateTime CreatedAtUtc);
