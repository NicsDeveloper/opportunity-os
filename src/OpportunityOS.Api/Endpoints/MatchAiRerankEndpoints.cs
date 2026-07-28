using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.AI;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Application.Pipeline;
using OpportunityOS.Application.Profiles;
using OpportunityOS.Application.Projections;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>
/// LLM-as-judge: re-pontua as N melhores vagas do feed do perfil com o motor de fit autoritativo
/// (understanding + fit). Caro por chamada — limitado a 20, com guarda de orçamento diário.
/// Caminho personal: auth + workspace-scoped via <c>ICurrentCandidateProfileProvider</c>.
/// </summary>
public static class MatchAiRerankEndpoints
{
    private const string CostCenter = "LlmRerank";
    private const int DefaultTake = 10;
    private const int MaxTake = 20;
    private const string LlmEngineVersion = "llm-fit-v2";

    public static void MapMatchAiRerankEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/matches/llm-rerank", async (
            Guid? candidateProfileId, int? take,
            OpportunityOsDbContext db, ICurrentCandidateProfileProvider profiles,
            IJobUnderstandingService understanding, ICandidateFitAnalysisService fit,
            ILatestOpportunityMatchProjection latest, IOpportunityPipeline pipeline,
            IQueryBudgetManager budget, ILlmProvider llm, CancellationToken ct) =>
        {
            var profile = await profiles.GetAsync(candidateProfileId, ct);
            if (profile is null) return Results.BadRequest(new { error = "No candidate profile registered." });
            if (!llm.IsConfigured)
                return Results.Ok(new { rescored = 0, changed = 0, skipped = 0, llmConfigured = false,
                    note = "LLM provider não está configurado; nada a fazer." });

            var n = Math.Clamp(take ?? DefaultTake, 1, MaxTake);

            // Pega o top-N atual do feed pela projeção (igual ao que o usuário vê), excluindo o que já
            // foi pontuado pelo LLM nesta geração — evita gastar budget repetido.
            var top = await db.LatestOpportunityMatches
                .Where(p => p.CandidateProfileId == profile.Id)
                .OrderByDescending(p => p.OverallScore)
                .ThenByDescending(p => p.UpdatedAtUtc)
                .Take(n * 2)
                .ToListAsync(ct);

            var jobIds = top.Select(t => t.JobPostingId).ToList();
            var jobs = await db.JobPostings
                .Where(j => jobIds.Contains(j.Id)
                    && j.Status != JobPostingStatus.Expired && j.Status != JobPostingStatus.Archived)
                .ToDictionaryAsync(j => j.Id, ct);

            // Mantém ordem do feed, descartando já-LLM e expiradas.
            var targets = top
                .Where(t => jobs.ContainsKey(t.JobPostingId) && t.EngineVersion != LlmEngineVersion)
                .Take(n).ToList();

            int processed = 0, changed = 0, skippedByBudget = 0, failed = 0;
            foreach (var t in targets)
            {
                if (!await budget.CanExecuteAsync(CostCenter, ct)) { skippedByBudget++; continue; }
                var job = jobs[t.JobPostingId];
                try
                {
                    var analysis = await understanding.AnalyzeAsync(job, ct);
                    job.ApplyNormalization(analysis.Seniority, analysis.WorkMode, analysis.Language,
                        analysis.RequiredSkills.Concat(analysis.NiceToHaveSkills), analysis.Domains);

                    var match = await fit.AnalyzeFitAsync(profile, job, analysis, ct);
                    job.MarkAnalyzed();
                    db.OpportunityMatches.Add(match);
                    await latest.UpsertAsync(match, ct);
                    await pipeline.EnsureForMatchAsync(match, ct);

                    await budget.RecordExecutionAsync(CostCenter, 1, ct);
                    processed++;
                    if (match.OverallScore != t.OverallScore) changed++;
                }
                catch
                {
                    failed++;
                }
            }
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { rescored = processed, changed, skippedByBudget, failed, llmConfigured = true });
        }).WithTags("AI Copilot").RequireAuthorization();
    }
}
