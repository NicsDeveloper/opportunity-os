using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.AI;
using OpportunityOS.Application.Matching;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

public static class AiEndpoints
{
    /// <summary>Minimum fit score required to draft outreach (spec criterion).</summary>
    private const int MinScoreForOutreach = 60;

    public static void MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs/{jobId:guid}/ai").WithTags("AI Copilot");

        // Understand the job (LLM) + score fit, persisting the resulting match.
        jobs.MapPost("/analyze", async (
            Guid jobId, OpportunityOsDbContext db, IJobUnderstandingService understanding,
            ICandidateFitAnalysisService fit, CancellationToken ct) =>
        {
            var job = await db.JobPostings.FindAsync([jobId], ct);
            if (job is null) return Results.NotFound();
            var profile = await ActiveProfile(db, ct);
            if (profile is null) return Results.BadRequest("No candidate profile registered.");

            var analysis = await understanding.AnalyzeAsync(job, ct);

            // Persist normalization derived from the analysis on the job.
            job.ApplyNormalization(analysis.Seniority, analysis.WorkMode, analysis.Language,
                analysis.RequiredSkills.Concat(analysis.NiceToHaveSkills), analysis.Domains);

            var match = await fit.AnalyzeFitAsync(profile, job, analysis, ct);
            job.MarkAnalyzed();
            db.OpportunityMatches.Add(match);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new AiAnalyzeResponse(analysis.ToResponse(), match.ToResponse()));
        });

        // Draft outreach (LinkedIn/email/cover/follow-up). Blocked below the score gate.
        jobs.MapPost("/generate-outreach", async (
            Guid jobId, OpportunityOsDbContext db, IOutreachDraftService outreach,
            IMatchEngine engine, ILlmProvider llm, CancellationToken ct) =>
        {
            var job = await db.JobPostings.FindAsync([jobId], ct);
            if (job is null) return Results.NotFound();
            var profile = await ActiveProfile(db, ct);
            if (profile is null) return Results.BadRequest("No candidate profile registered.");

            var match = await GetOrCreateMatch(db, engine, job, profile, ct);
            if (match.OverallScore < MinScoreForOutreach)
                return Results.UnprocessableEntity(
                    $"Match score {match.OverallScore} is below the minimum of {MinScoreForOutreach}; no outreach generated.");

            var draft = await outreach.GenerateAsync(profile, job, match, ct);
            var message = new GeneratedMessage(
                job.Id, match.Id, draft.LinkedInMessage, draft.CoverLetter, draft.EmailSubject,
                draft.EmailBody, draft.CvTailoringNotes, draft.FollowUpMessage, draft.HumanReviewNotes,
                Prompts.OutreachVersion, llm.ModelName);
            db.GeneratedMessages.Add(message);
            await db.SaveChangesAsync(ct);

            return Results.Ok(message.ToResponse());
        });

        // CV tailoring suggestions (advisory; never mutates the CV).
        jobs.MapPost("/suggest-cv-tailoring", async (
            Guid jobId, OpportunityOsDbContext db, ICvTailoringSuggestionService cv,
            IMatchEngine engine, CancellationToken ct) =>
        {
            var job = await db.JobPostings.FindAsync([jobId], ct);
            if (job is null) return Results.NotFound();
            var profile = await ActiveProfile(db, ct);
            if (profile is null) return Results.BadRequest("No candidate profile registered.");

            var match = await GetOrCreateMatch(db, engine, job, profile, ct);
            var suggestion = await cv.SuggestAsync(profile, job, match, ct);
            return Results.Ok(suggestion.ToResponse());
        });

        // Career insights across analyzed jobs.
        app.MapPost("/api/insights/career", async (
            CareerInsightRequest? req, OpportunityOsDbContext db, ICareerInsightService insights,
            CancellationToken ct) =>
        {
            var profile = await ActiveProfile(db, ct);
            if (profile is null) return Results.BadRequest("No candidate profile registered.");

            var take = Math.Clamp(req?.MaxJobs ?? 50, 1, 200);
            var analyzedJobs = await db.JobPostings
                .Where(j => j.Status >= Domain.Enums.JobPostingStatus.Analyzed)
                .OrderByDescending(j => j.UpdatedAtUtc)
                .Take(take)
                .ToListAsync(ct);

            var report = await insights.GenerateInsightsAsync(profile, analyzedJobs, ct);
            return Results.Ok(report.ToResponse());
        }).WithTags("AI Copilot");
    }

    private static Task<CandidateProfile?> ActiveProfile(OpportunityOsDbContext db, CancellationToken ct) =>
        db.CandidateProfiles.OrderByDescending(p => p.CreatedAtUtc).FirstOrDefaultAsync(ct);

    /// <summary>Latest persisted match for the job, or a freshly computed+persisted heuristic one.</summary>
    private static async Task<OpportunityMatch> GetOrCreateMatch(
        OpportunityOsDbContext db, IMatchEngine engine, JobPosting job, CandidateProfile profile, CancellationToken ct)
    {
        var existing = await db.OpportunityMatches
            .Where(m => m.JobPostingId == job.Id)
            .OrderByDescending(m => m.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        var match = engine.Evaluate(profile, job).ToEntity(job.Id, profile.Id);
        db.OpportunityMatches.Add(match);
        await db.SaveChangesAsync(ct);
        return match;
    }
}
