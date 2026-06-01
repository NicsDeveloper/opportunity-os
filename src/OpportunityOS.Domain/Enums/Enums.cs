namespace OpportunityOS.Domain.Enums;

public enum CompanyPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Strategic = 4
}

public enum CompanySource
{
    Manual = 1,
    CsvImport = 2,
    SearchEngine = 3,
    PublicRegistry = 4,
    AtsDiscovery = 5,
    Bacen = 6
}

public enum JobPostingStatus
{
    Discovered = 1,
    Normalized = 2,
    Analyzed = 3,
    Shortlisted = 4,
    Rejected = 5,
    Archived = 6,
    Expired = 7
}

public enum MatchRecommendation
{
    Ignore = 1,
    SaveForLater = 2,
    Apply = 3,
    Prioritize = 4,
    Strategic = 5
}

public enum GeneratedMessageStatus
{
    Draft = 1,
    Reviewed = 2,
    Used = 3,
    Discarded = 4
}

public enum OpportunityStatus
{
    Discovered = 1,
    Analyzed = 2,
    MessageGenerated = 3,
    ReadyForHumanReview = 4,
    SentManually = 5,
    AppliedManually = 6,
    WaitingResponse = 7,
    InterviewScheduled = 8,
    Rejected = 9,
    Archived = 10
}

public enum RecruiterLeadSource
{
    Manual = 1,
    UserProvided = 2,
    PublicCompanyPage = 3,
    Referral = 4
}

public enum ExecutionRunStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    PartiallyFailed = 4
}

// ---------- Firehose (massive discovery) ----------

public enum SearchCampaignStatus
{
    Active = 1,
    Paused = 2,
    Archived = 3
}

public enum SearchCampaignPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Aggressive = 4
}

public enum SearchQueryExecutionStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

/// <summary>Where a candidate was found — drives trust and how it must be handled.</summary>
public enum SourceType
{
    OfficialAts = 1,
    OfficialCareerPage = 2,
    JobBoard = 3,
    Aggregator = 4,
    SearchResult = 5,
    SocialIndexed = 6,
    Unknown = 7
}

public enum RawJobCandidateStatus
{
    Discovered = 1,
    Classified = 2,
    Enriched = 3,
    Duplicate = 4,
    PromotedToJobPosting = 5,
    Rejected = 6,
    Expired = 7
}

public enum UserFeedbackType
{
    Relevant = 1,
    Irrelevant = 2,
    HideSimilar = 3,
    BadCompanyDetection = 4,
    BadScore = 5,
    Duplicate = 6,
    Expired = 7,
    InterestingCompany = 8,
    Applied = 9,
    ContactedRecruiter = 10
}
