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
    List<CandidateExperienceDto>? Experiences,
    // Multi-profile fields (optional for back-compat with existing callers/tests).
    string? DisplayName = null,
    List<string>? ExcludedStacks = null,
    List<string>? PreferredWorkModes = null,
    int? MinimumScoreToShow = null);

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
    DateTime UpdatedAtUtc,
    string DisplayName,
    bool IsDefault,
    List<string> ExcludedStacks,
    List<string> PreferredWorkModes,
    int MinimumScoreToShow);

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
    string? CompanyWebsiteUrl, DateTime PostedAtUtc, string Rationale,
    int DiscoveryRank, string SourceType, int SourceConfidenceScore,
    bool RequiresManualValidation, string? RealCompanyName, string? SourceName,
    bool DatePrecise);

// One opportunity the user already acted on (applications board).
public sealed record ApplicationResponse(
    Guid JobPostingId, string JobTitle, string CompanyName, string JobUrl,
    string? CompanyWebsiteUrl, int OverallScore, string Action,
    DateTime AppliedAtUtc, DateTime PostedAtUtc);

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
    string? Notes,
    Guid CandidateProfileId);

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

// ---- Firehose (massive discovery) ----

public sealed record CreateCampaignRequest(
    string Name,
    string? Description,
    string? Priority,
    List<string> BaseKeywords,
    List<string>? TargetSources,
    List<string>? ExcludedDomains,
    int? DailyQueryBudget);

public sealed record SearchCampaignResponse(
    Guid Id,
    string Name,
    string Description,
    string Status,
    string Priority,
    List<string> BaseKeywords,
    List<string> TargetSources,
    List<string> ExcludedDomains,
    int DailyQueryBudget,
    DateTime CreatedAtUtc,
    DateTime? LastRunAtUtc);

public sealed record QuickSearchRequest(
    string Query,
    int? Take,
    bool? SaveRawCandidates);

public sealed record AggressiveSearchRequest(
    Guid? CampaignId,
    int? MaxQueries,
    int? MaxResultsPerQuery,
    bool? SaveRawCandidates,
    bool? PromoteAutomatically);

public sealed record FirehoseRunResponse(
    Guid ExecutionRunId,
    string Status,
    Guid? CampaignId,
    int QueriesExecuted,
    int ResultsCount,
    int NewCandidates,
    int Duplicates,
    int Errors);

public sealed record RawJobCandidateResponse(
    Guid Id,
    string Title,
    string? Snippet,
    string DiscoveredUrl,
    string SourceProvider,
    string SourceName,
    string SourceType,
    string? RealCompanyName,
    string? OriginalJobUrl,
    string? Location,
    string? WorkMode,
    string? Language,
    DateTime DiscoveredAtUtc,
    DateTime? PublishedAtUtc,
    string Status,
    int SourceConfidenceScore,
    int? PreliminaryFitScore,
    bool RequiresManualValidation,
    Guid? SearchCampaignId,
    string? Query);

// ---- Firehose promotion (RawJobCandidate -> JobPosting) ----

public sealed record PromoteBatchRequest(
    int? MaxCandidates,
    int? MinSourceConfidence,
    bool? RequireRealCompany);

public sealed record PromotionResultResponse(
    bool Promoted,
    Guid? JobPostingId,
    bool WasDuplicate,
    string Reason);

public sealed record BatchPromotionResponse(
    int Considered,
    int Promoted,
    int Duplicates,
    int Skipped);

// ---- Feedback + metrics (P11/P16/P12) ----

public sealed record FeedbackRequest(
    string Type,
    Guid? JobPostingId,
    Guid? RawJobCandidateId,
    string? Reason,
    Guid? CandidateProfileId = null);

public sealed record DiscoveryMetricsResponse(
    int RawCandidatesToday,
    int RawCandidatesThisWeek,
    int QueriesToday,
    int JobsPromotedToday,
    double DeduplicationRate,
    int AverageSourceConfidence,
    int AverageFitScore,
    int ActionableOpportunities,
    int WeakSources,
    int RelevantFeedback,
    int IrrelevantFeedback,
    Dictionary<string, int> BySourceType);

public sealed record ProviderQualityResponse(
    string Provider,
    int QueriesExecuted,
    int RawCandidates,
    int PromotedJobs,
    double DuplicateRate,
    int AverageSourceConfidence);

// ---- Bacen Financial Sweep (P2) ----

public sealed record BacenSweepRequest(
    string? MinimumPriority,
    int? MaxCompanies,
    int? MaxQueriesPerCompany,
    bool? IncludeCooperatives,
    bool? SaveRawCandidates);

public sealed record BacenSweepPreviewResponse(
    int Companies, int Strategic, int High, int Medium, int Low,
    int EstimatedQueries, int EstimatedBudgetCost);

// ---- Consulting Radar (P3) ----

public sealed record ConsultingDiscoverRequest(int? MaxQueries, bool? IncludeSeeds);

public sealed record ConsultingDiscoverResponse(
    Guid ExecutionRunId, string Status, int QueriesExecuted, int CandidatesFound, int Duplicates);

public sealed record ConsultingCandidateResponse(
    Guid Id, string Name, string? WebsiteUrl, string? LinkedInCompanyUrl, string Country,
    string Source, List<string> Signals, int ConsultingConfidenceScore, string Status,
    DateTime CreatedAtUtc, DateTime? PromotedAtUtc);

public sealed record PromoteConsultingBatchRequest(int? MinConfidence, int? Max);

public sealed record ConsultingPromotionResponse(int Considered, int Promoted, int Skipped);
