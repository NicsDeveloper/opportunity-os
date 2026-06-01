using OpportunityOS.Application.Normalization;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Matching;

/// <summary>
/// Phase 1 heuristic match engine (no LLM). Every sub-score is derived from
/// transparent keyword signals so the rationale can be shown to the user.
///
/// OverallScore = Tech*0.35 + Domain*0.25 + Seniority*0.15 + Location*0.15 + Language*0.10
/// </summary>
public sealed class HeuristicMatchEngine : IMatchEngine
{
    private readonly IJobNormalizer _normalizer;

    public HeuristicMatchEngine(IJobNormalizer normalizer) => _normalizer = normalizer;

    public MatchResult Evaluate(CandidateProfile profile, JobPosting job)
    {
        var haystack = $"{job.Title} {job.Location} {job.DescriptionText}".ToLowerInvariant();
        var norm = _normalizer.Normalize(job);

        var strengths = new List<string>();
        var risks = new List<string>();
        var missing = new List<string>();

        var technical = ScoreTechnical(haystack, strengths, risks, missing);
        var domain = ScoreDomain(haystack, strengths, risks);
        var seniority = ScoreSeniority(norm.Seniority, strengths, risks);
        var location = ScoreLocation(norm.WorkMode, haystack, strengths, risks);
        var language = ScoreLanguage(norm.Language ?? job.Language, profile, risks);

        // Technical fit dominates: a clear .NET/backend role is a strong match for this
        // candidate even outside payments. Domain is a bonus, not a gate.
        var overall = (int)Math.Round(
            technical * 0.45 +
            domain * 0.20 +
            seniority * 0.15 +
            location * 0.10 +
            language * 0.10,
            MidpointRounding.AwayFromZero);

        var recommendation = Recommend(overall);
        var rationale = BuildRationale(overall, technical, domain, seniority, location, language);

        return new MatchResult(
            overall, technical, domain, seniority, location, language,
            recommendation, strengths, risks, missing, rationale);
    }

    private static int ScoreTechnical(string haystack, List<string> strengths, List<string> risks, List<string> missing)
    {
        var hasDotNet = ContainsAny(haystack, KnownTerms.CoreDotNet);
        var hasBackend = ContainsAny(haystack, KnownTerms.BackendSignals);
        var bonusCount = CountDistinct(haystack, KnownTerms.BonusStack);
        var competing = CountDistinct(haystack, KnownTerms.CompetingLanguages);
        var hasFrontend = ContainsAny(haystack, KnownTerms.FrontendSignals);

        int score;
        if (hasDotNet)
        {
            score = 65 + Math.Min(bonusCount * 8, 35);
            strengths.Add("Stack .NET/C# explícita na vaga");
            if (bonusCount > 0) strengths.Add("Stack de apoio compatível (cloud/mensageria/dados)");
            score -= Math.Min(competing * 5, 15); // polyglot shops dilute fit slightly
        }
        else if (hasBackend)
        {
            score = 48 + Math.Min(bonusCount * 6, 24);
            risks.Add("Vaga backend sem .NET explícito; confirmar stack principal");
            missing.Add(".NET/C# não citado explicitamente");
            score -= Math.Min(competing * 8, 24);
        }
        else
        {
            score = hasFrontend ? 10 : 22;
            risks.Add("Stack principal aparenta não ser backend .NET");
            missing.Add("Aderência técnica baixa ao perfil backend .NET");
            score -= Math.Min(competing * 6, 18);
        }

        return Clamp(score);
    }

    private static int ScoreDomain(string haystack, List<string> strengths, List<string> risks)
    {
        var strong = CountDistinct(haystack, KnownTerms.StrongDomains);
        var medium = CountDistinct(haystack, KnownTerms.MediumDomains);

        if (strong > 0)
        {
            strengths.Add("Domínio financeiro/pagamentos (área assimétrica do candidato)");
            return Clamp(82 + Math.Min(strong * 4, 18));
        }
        if (medium > 0)
        {
            risks.Add("Domínio adjacente (não-core financeiro)");
            return Clamp(60 + Math.Min(medium * 4, 15));
        }
        // No domain signal isn't a red flag for a backend role — pagamentos é bônus, não gate.
        return 50;
    }

    private static int ScoreSeniority(string? seniority, List<string> strengths, List<string> risks)
    {
        switch (seniority)
        {
            case "Senior":
            case "MidLevel":
                strengths.Add($"Senioridade compatível ({seniority})");
                return 90;
            case "Lead":
                return 70;
            case "Staff":
            case "Principal":
                risks.Add($"Senioridade acima do alvo ({seniority})");
                return 50;
            case "Junior":
            case "Intern":
                risks.Add($"Senioridade abaixo do alvo ({seniority})");
                return 30;
            default:
                return 65; // not informed
        }
    }

    private static int ScoreLocation(string? workMode, string haystack, List<string> strengths, List<string> risks)
    {
        var brazilOrGlobal = haystack.Contains("brazil", StringComparison.Ordinal)
            || haystack.Contains("brasil", StringComparison.Ordinal)
            || haystack.Contains("latam", StringComparison.Ordinal)
            || haystack.Contains("global", StringComparison.Ordinal)
            || haystack.Contains("worldwide", StringComparison.Ordinal);

        switch (workMode)
        {
            case "Remote":
                strengths.Add("Modelo remoto");
                return brazilOrGlobal ? 95 : 85;
            case "Hybrid":
                return 65;
            case "Onsite":
                risks.Add("Modelo presencial; validar localização");
                return 35;
            default:
                return 60; // unknown
        }
    }

    private static int ScoreLanguage(string? language, CandidateProfile profile, List<string> risks)
    {
        if (string.IsNullOrEmpty(language)) return 70;
        if (language is "pt-BR" or "en") return 90;
        risks.Add($"Idioma da vaga ({language}) pode não ser compatível");
        return 40;
    }

    private static MatchRecommendation Recommend(int overall) => overall switch
    {
        >= 90 => MatchRecommendation.Strategic,
        >= 75 => MatchRecommendation.Prioritize,
        >= 60 => MatchRecommendation.Apply,
        >= 40 => MatchRecommendation.SaveForLater,
        _ => MatchRecommendation.Ignore
    };

    private static string BuildRationale(int overall, int tech, int domain, int sen, int loc, int lang) =>
        $"Score {overall}/100 — Técnico {tech}, Domínio {domain}, Senioridade {sen}, " +
        $"Localização {loc}, Idioma {lang}.";

    private static bool ContainsAny(string haystack, string[] tokens) =>
        tokens.Any(t => haystack.Contains(t, StringComparison.Ordinal));

    private static int CountDistinct(string haystack, string[] tokens) =>
        tokens.Count(t => haystack.Contains(t, StringComparison.Ordinal));

    private static int Clamp(int v) => Math.Clamp(v, 0, 100);
}
