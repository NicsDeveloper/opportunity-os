namespace OpportunityOS.Application.AI;

/// <summary>Structured understanding of a job description (from §9.6).</summary>
public sealed record JobAnalysisResult(
    List<string> RequiredSkills,
    List<string> NiceToHaveSkills,
    List<string> Domains,
    string Seniority,
    string WorkMode,
    string Language,
    List<string> Responsibilities,
    List<string> Risks,
    string Summary);

/// <summary>Outreach drafts for human review. Nothing here is ever sent automatically.</summary>
public sealed record GeneratedOutreachResult(
    string LinkedInMessage,
    string CoverLetter,
    string EmailSubject,
    string EmailBody,
    string CvTailoringNotes,
    string FollowUpMessage,
    string HumanReviewNotes);

/// <summary>CV tailoring recommendations. Advisory only — never mutates the CV.</summary>
public sealed record CvTailoringSuggestion(
    string SummaryAdjustment,
    List<string> SkillsToHighlight,
    List<string> KeywordsToInclude,
    List<string> BulletSuggestions,
    List<string> SectionsToReorder,
    string Notes);

/// <summary>Aggregated career insights across many analyzed jobs.</summary>
public sealed record CareerInsightReport(
    List<string> MostRequestedTechnologies,
    List<string> RecurringGaps,
    List<string> StrongestDomains,
    List<string> StudySuggestions,
    List<string> PostIdeas,
    List<string> MostPromisingCompanies,
    string Summary);
