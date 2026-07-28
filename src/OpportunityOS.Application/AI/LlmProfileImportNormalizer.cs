using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Import;
using OpportunityOS.Contracts;

namespace OpportunityOS.Application.AI;

/// <summary>
/// Optional LLM refinement of the deterministic draft, gated by feature flag. STRICTLY conservative:
/// the model may only re-classify/normalize skills, domains, seniority, roles and preferences. It must
/// not touch experiences/education/identity — and we enforce that by re-grafting those FACTS verbatim
/// from the deterministic draft after the call, so invention is structurally impossible. Any failure
/// falls back to the deterministic draft (via <see cref="AiServiceBase"/>).
/// </summary>
public sealed class LlmProfileImportNormalizer : AiServiceBase, IProfileImportNormalizer
{
    public LlmProfileImportNormalizer(ILlmProvider llm, IPromptExecutionLogStore audit, ILogger<LlmProfileImportNormalizer> logger)
        : base(llm, audit, logger) { }

    protected override string ServiceName => "ProfileImportNormalizer";

    /// <summary>Only the classifiable fields the LLM is allowed to influence.</summary>
    private sealed record Fields(
        string? Seniority, string? DisplayName,
        List<string>? CoreSkills, List<string>? SecondarySkills, List<string>? ExcludedStacks,
        List<string>? Domains, List<string>? PreferredRoles,
        List<string>? PreferredContractTypes, List<string>? PreferredWorkModes, int? MinimumScoreToShow);

    public async Task<CandidateProfileDraftDto> NormalizeAsync(
        LinkedInProfileImportDto parsed, CandidateProfileDraftDto d, CancellationToken ct)
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var context = JsonSerializer.Serialize(new
        {
            parsed.Headline, parsed.Summary, parsed.Skills,
            ExperienceTitles = parsed.Experiences.Select(e => $"{e.Title} @ {e.Company}"),
            current = new { d.Seniority, d.DisplayName, d.CoreSkills, d.SecondarySkills, d.Domains, d.PreferredRoles, d.PreferredWorkModes }
        }, opts);

        var request = new LlmRequest(
            SystemPrompt:
                "Você normaliza um rascunho de perfil profissional para um radar de vagas. " +
                "REGRAS INVIOLÁVEIS: não invente, não altere e não remova experiências, empresas, cargos ou datas. " +
                "Use SOMENTE o que aparece no texto fornecido. Você pode APENAS reclassificar/normalizar: " +
                "skills (core vs secundárias), domínios, senioridade, cargos desejados e preferências. " +
                "Não infira Java a partir de JavaScript. Responda SOMENTE com JSON válido.",
            UserPrompt:
                $$"""
                Com base no contexto, retorne JSON com EXATAMENTE estas chaves (sem experiências):
                {
                  "seniority": "", "displayName": "",
                  "coreSkills": [], "secondarySkills": [], "excludedStacks": [],
                  "domains": [], "preferredRoles": [],
                  "preferredContractTypes": [], "preferredWorkModes": [], "minimumScoreToShow": 60
                }

                Contexto:
                {{context}}
                """,
            PromptVersion: Prompts.ProfileImportVersion);

        // Fallback returns the deterministic classifications, so the merge is always well-formed.
        var fallback = new Fields(d.Seniority, d.DisplayName, d.CoreSkills, d.SecondarySkills, d.ExcludedStacks,
            d.Domains, d.PreferredRoles, d.PreferredContractTypes, d.PreferredWorkModes, d.MinimumScoreToShow);

        var f = await ExecuteAsync(request, () => fallback, jobId: null, profileId: null, ct);

        // Merge: classifications from the LLM (or fallback); FACTS always from the deterministic draft.
        return d with
        {
            Seniority = NonEmpty(f.Seniority) ?? d.Seniority,
            DisplayName = NonEmpty(f.DisplayName) ?? d.DisplayName,
            CoreSkills = NonEmpty(f.CoreSkills) ?? d.CoreSkills,
            SecondarySkills = NonEmpty(f.SecondarySkills) ?? d.SecondarySkills,
            ExcludedStacks = f.ExcludedStacks ?? d.ExcludedStacks,
            Domains = NonEmpty(f.Domains) ?? d.Domains,
            PreferredRoles = NonEmpty(f.PreferredRoles) ?? d.PreferredRoles,
            PreferredContractTypes = NonEmpty(f.PreferredContractTypes) ?? d.PreferredContractTypes,
            PreferredWorkModes = NonEmpty(f.PreferredWorkModes) ?? d.PreferredWorkModes,
            MinimumScoreToShow = f.MinimumScoreToShow is > 0 and <= 100 ? f.MinimumScoreToShow.Value : d.MinimumScoreToShow,
            // FACTS — never from the LLM:
            FullName = d.FullName, Headline = d.Headline, Summary = d.Summary,
            Location = d.Location, PreferredLocations = d.PreferredLocations, Experiences = d.Experiences,
        };
    }

    private static string? NonEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
    private static List<string>? NonEmpty(List<string>? l) => l is { Count: > 0 } ? l : null;
}
