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

// ---- Discovery ----

public sealed record DiscoverRequest(Guid? CompanyId);

public sealed record ValidateLinksResponse(int Checked, int Expired);

public sealed record SearchRequest(List<string>? Keywords);

public sealed record AtsDetectionResponse(
    bool Detected,
    string? Ats,
    string? BoardUrl,
    string? Token,
    string? CareersPageUrl,
    bool ProviderSupported);

public sealed record CsvImportResponse(int Created);

public sealed record OnboardingResponse(
    Guid ExecutionRunId, string Status, int Processed, int BoardsFound, int Errors);

public sealed record WebsiteDiscoveryResponse(bool Found, string? WebsiteUrl);

public sealed record BackfillResponse(int Processed, int Found);

// ---- Bacen ----

public sealed record BacenImportResponse(
    int TotalRead, int Created, int Updated, int Skipped, List<string> Warnings);

public sealed record BacenPromotionResponse(
    int TotalEligible, int CompaniesCreated, int CompaniesUpdated, int Skipped, List<string> Warnings);

public sealed record BacenInstitutionResponse(
    Guid Id,
    string Name,
    string? Ispb,
    string? Cnpj,
    string InstitutionType,
    bool AuthorizedByBacen,
    string? SpiParticipationType,
    string? PixParticipationType,
    string? PixParticipationMode,
    bool? PaymentInitiation,
    bool? CashoutServiceFacilitator,
    List<string> Tags,
    DateTime ImportedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record DiscoveryResultResponse(
    Guid ExecutionRunId,
    string Status,
    int CompaniesProcessed,
    int ProvidersInvoked,
    int JobsDiscovered,
    int JobsUpdated,
    int Errors);

// ---- Dashboard ----

public sealed record DashboardSummaryResponse(
    int JobsDiscovered, int JobsToday,
    int MatchesAbove75, int MatchesAbove75Today,
    int MessagesGenerated, int MessagesToday,
    int EmailsSent, int EmailsToday,
    int FollowUpsPending, int? NextFollowUpInDays);

public sealed record BestOpportunityResponse(
    Guid MatchId, Guid JobPostingId, string JobTitle, string CompanyName,
    List<string> Skills, int OverallScore, string Recommendation, string JobUrl,
    string? CompanyWebsiteUrl, DateTime PostedAtUtc, string Rationale);

public sealed record ExecutionRunResponse(
    Guid Id, string RunType, string Status, DateTime StartedAtUtc,
    int ItemsProcessed, int ItemsSucceeded, int ItemsFailed);

public sealed record GeneratedMessageSummary(
    Guid Id, Guid JobPostingId, string EmailSubject, string Status, DateTime CreatedAtUtc);

// ---- Opportunities (pipeline) ----

public sealed record OpportunityResponse(
    Guid Id,
    Guid JobPostingId,
    Guid? RecruiterLeadId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? LastActionAtUtc,
    DateTime? NextFollowUpAtUtc,
    string? Notes);

public sealed record OpportunityStatusRequest(string Status);
public sealed record OpportunityNotesRequest(string? Notes);
public sealed record OpportunityFollowUpRequest(DateTime? NextFollowUpAtUtc);

// ---- Recruiter leads ----

public sealed record RecruiterRequest(
    Guid CompanyId,
    string FullName,
    string? RoleTitle,
    string? LinkedInUrl,
    string? Email,
    int? Source,
    string? Notes);

public sealed record RecruiterResponse(
    Guid Id,
    Guid CompanyId,
    string FullName,
    string? RoleTitle,
    string? LinkedInUrl,
    string? Email,
    string Source,
    string? Notes,
    DateTime CreatedAtUtc);

// ---- Digest ----

public sealed record DigestPreviewResponse(
    string Subject,
    string Markdown,
    string Html,
    int Total,
    int StrategicCount,
    int PrioritizeCount,
    int ApplyCount);

public sealed record DigestSendResponse(
    bool Sent,
    string Reason,
    int ItemCount,
    Guid ExecutionRunId);

// ---- AI Copilot ----

public sealed record JobAnalysisResponse(
    List<string> RequiredSkills,
    List<string> NiceToHaveSkills,
    List<string> Domains,
    string Seniority,
    string WorkMode,
    string Language,
    List<string> Responsibilities,
    List<string> Risks,
    string Summary);

public sealed record AiAnalyzeResponse(JobAnalysisResponse Analysis, MatchResponse Match);

public sealed record GeneratedMessageResponse(
    Guid Id,
    Guid JobPostingId,
    Guid OpportunityMatchId,
    string LinkedInMessage,
    string CoverLetter,
    string EmailSubject,
    string EmailBody,
    string CvTailoringNotes,
    string FollowUpMessage,
    string HumanReviewNotes,
    string Status,
    string PromptVersion,
    string ModelName,
    DateTime CreatedAtUtc);

public sealed record CvTailoringResponse(
    string SummaryAdjustment,
    List<string> SkillsToHighlight,
    List<string> KeywordsToInclude,
    List<string> BulletSuggestions,
    List<string> SectionsToReorder,
    string Notes);

public sealed record CareerInsightRequest(int? MaxJobs);

public sealed record CareerInsightResponse(
    List<string> MostRequestedTechnologies,
    List<string> RecurringGaps,
    List<string> StrongestDomains,
    List<string> StudySuggestions,
    List<string> PostIdeas,
    List<string> MostPromisingCompanies,
    string Summary);

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
