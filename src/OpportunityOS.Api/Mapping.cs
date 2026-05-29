using OpportunityOS.Application.AI;
using OpportunityOS.Application.Matching;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Api;

/// <summary>Entity &lt;-&gt; DTO mapping for the API surface.</summary>
public static class Mapping
{
    public static CandidateExperienceDto ToDto(this CandidateExperience e) =>
        new(e.Company, e.Role, e.Period, e.Technologies, e.Domains, e.Achievements);

    public static CandidateExperience ToDomain(this CandidateExperienceDto d) =>
        new()
        {
            Company = d.Company,
            Role = d.Role,
            Period = d.Period,
            Technologies = d.Technologies ?? new(),
            Domains = d.Domains ?? new(),
            Achievements = d.Achievements ?? new()
        };

    public static CandidateProfileResponse ToResponse(this CandidateProfile p) =>
        new(p.Id, p.FullName, p.Headline, p.Summary, p.Location, p.Seniority, p.PreferredLanguage,
            p.CoreSkills, p.SecondarySkills, p.Domains, p.PreferredRoles, p.PreferredContractTypes,
            p.PreferredLocations, p.Experiences.Select(ToDto).ToList(), p.CreatedAtUtc, p.UpdatedAtUtc);

    public static CompanyResponse ToResponse(this Company c) =>
        new(c.Id, c.Name, c.WebsiteUrl, c.CareersUrl, c.LinkedInUrl, c.Industry, c.Country,
            c.Priority.ToString(), c.Source.ToString(), c.Tags, c.CreatedAtUtc, c.LastScannedAtUtc);

    public static JobPostingResponse ToResponse(this JobPosting j) =>
        new(j.Id, j.CompanyId, j.ExternalId, j.SourceProvider, j.Title, j.Department, j.Location,
            j.WorkMode, j.Seniority, j.Language, j.AbsoluteUrl, j.DescriptionText, j.ExtractedSkills,
            j.ExtractedDomains, j.Status.ToString(), j.PublishedAtUtc, j.CreatedAtUtc, j.UpdatedAtUtc);

    public static MatchResponse ToResponse(this OpportunityMatch m) =>
        new(m.Id, m.JobPostingId, m.CandidateProfileId, m.OverallScore, m.TechnicalScore, m.DomainScore,
            m.SeniorityScore, m.LocationScore, m.LanguageScore, m.Recommendation.ToString(),
            m.Strengths, m.Risks, m.MissingRequirements, m.Rationale, m.CreatedAtUtc);

    public static OpportunityMatch ToEntity(this MatchResult r, Guid jobId, Guid profileId) =>
        new(jobId, profileId, r.OverallScore, r.TechnicalScore, r.DomainScore, r.SeniorityScore,
            r.LocationScore, r.LanguageScore, r.Recommendation, r.Strengths, r.Risks,
            r.MissingRequirements, r.Rationale);

    public static JobAnalysisResponse ToResponse(this JobAnalysisResult a) =>
        new(a.RequiredSkills, a.NiceToHaveSkills, a.Domains, a.Seniority, a.WorkMode, a.Language,
            a.Responsibilities, a.Risks, a.Summary);

    public static GeneratedMessageResponse ToResponse(this GeneratedMessage m) =>
        new(m.Id, m.JobPostingId, m.OpportunityMatchId, m.LinkedInMessage, m.CoverLetter,
            m.EmailSubject, m.EmailBody, m.CvTailoringNotes, m.FollowUpMessage, m.HumanReviewNotes,
            m.Status.ToString(), m.PromptVersion, m.ModelName, m.CreatedAtUtc);

    public static CvTailoringResponse ToResponse(this CvTailoringSuggestion s) =>
        new(s.SummaryAdjustment, s.SkillsToHighlight, s.KeywordsToInclude, s.BulletSuggestions,
            s.SectionsToReorder, s.Notes);

    public static CareerInsightResponse ToResponse(this CareerInsightReport r) =>
        new(r.MostRequestedTechnologies, r.RecurringGaps, r.StrongestDomains, r.StudySuggestions,
            r.PostIdeas, r.MostPromisingCompanies, r.Summary);

    public static OpportunityResponse ToResponse(this Opportunity o) =>
        new(o.Id, o.JobPostingId, o.RecruiterLeadId, o.Status.ToString(), o.CreatedAtUtc,
            o.LastActionAtUtc, o.NextFollowUpAtUtc, o.Notes);

    public static RecruiterResponse ToResponse(this RecruiterLead r) =>
        new(r.Id, r.CompanyId, r.FullName, r.RoleTitle, r.LinkedInUrl, r.Email,
            r.Source.ToString(), r.Notes, r.CreatedAtUtc);

    public static BacenInstitutionResponse ToResponse(this BacenInstitution i) =>
        new(i.Id, i.Name, i.Ispb, i.Cnpj, i.InstitutionType, i.AuthorizedByBacen,
            i.SpiParticipationType, i.PixParticipationType, i.PixParticipationMode,
            i.PaymentInitiation, i.CashoutServiceFacilitator, i.Tags, i.ImportedAtUtc, i.UpdatedAtUtc);
}
