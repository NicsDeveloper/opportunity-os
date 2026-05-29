using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

public sealed class OutreachDraftService : AiServiceBase, IOutreachDraftService
{
    public OutreachDraftService(
        ILlmProvider llm, IPromptExecutionLogStore audit, ILogger<OutreachDraftService> logger)
        : base(llm, audit, logger) { }

    protected override string ServiceName => "OutreachDraft";

    public Task<GeneratedOutreachResult> GenerateAsync(
        CandidateProfile profile, JobPosting job, OpportunityMatch match, CancellationToken ct) =>
        ExecuteAsync(
            Prompts.Outreach(profile, job, match),
            () => HeuristicFallback(profile, job, match),
            job.Id, profile.Id, ct);

    /// <summary>
    /// Deterministic, fact-only draft (no LLM). Uses only the profile's own data so
    /// nothing is invented; clearly marked for human review.
    /// </summary>
    private static GeneratedOutreachResult HeuristicFallback(
        CandidateProfile profile, JobPosting job, OpportunityMatch match)
    {
        var topSkills = string.Join(", ", profile.CoreSkills.Take(4));
        var topDomains = string.Join(", ", profile.Domains.Take(3));

        var linkedIn =
            $"Olá! Vi a vaga \"{job.Title}\" e meu perfil tem aderência: {profile.Headline}, " +
            $"com experiência em {topSkills}" +
            (string.IsNullOrEmpty(topDomains) ? "" : $" e domínios como {topDomains}") +
            ". Podemos conversar sobre a posição?";

        var cover =
            $"Tenho interesse na vaga \"{job.Title}\". Atuo como {profile.Headline} e trabalho com " +
            $"{topSkills}. {profile.Summary} Acredito que minha experiência se conecta com o que a posição exige. " +
            "Fico à disposição para detalhar como posso contribuir.";

        return new GeneratedOutreachResult(
            LinkedInMessage: Trim(linkedIn, 700),
            CoverLetter: cover,
            EmailSubject: $"Candidatura — {job.Title}",
            EmailBody: cover,
            CvTailoringNotes: $"Destacar no CV: {topSkills}.",
            FollowUpMessage: "Olá! Reforçando meu interesse na vaga. Houve oportunidade de avaliar meu perfil?",
            HumanReviewNotes: "Rascunho heurístico (sem LLM). Revise e personalize antes de enviar. " +
                              $"Score de aderência: {match.OverallScore}/100.");
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
