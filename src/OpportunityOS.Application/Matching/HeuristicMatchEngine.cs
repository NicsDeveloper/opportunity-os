using OpportunityOS.Application.Normalization;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Matching;

/// <summary>
/// Profile-driven heuristic match engine (no LLM). Every sub-score is derived from
/// transparent keyword signals so the rationale can be shown to the user.
///
/// TechnicalFit is computed against the CANDIDATE'S stack (via <see cref="StackTaxonomy"/>),
/// not a hardcoded .NET bias — so the same job scores high for a Java profile on a Java role
/// and low for a Frontend profile on a backend role.
///
/// OverallScore = Tech*0.50 + Role*0.15 + Seniority*0.10 + Domain*0.10 + Location*0.10 + Language*0.05
/// </summary>
public sealed class HeuristicMatchEngine : IMatchEngine
{
    /// <summary>Version tag persisted on produced matches (lets re-scoring target a generation).</summary>
    public const string Version = "heuristic-v2";

    private readonly IJobNormalizer _normalizer;

    public HeuristicMatchEngine(IJobNormalizer normalizer) => _normalizer = normalizer;

    public MatchResult Evaluate(CandidateProfile profile, JobPosting job)
    {
        var titleLower = job.Title.ToLowerInvariant();
        var haystack = $"{job.Title} {job.Location} {job.DescriptionText}".ToLowerInvariant();
        var norm = _normalizer.Normalize(job);

        var sig = StackTaxonomy.Detect(profile);
        var jobFamilies = StackTaxonomy.FamiliesIn(haystack);
        var titleFamilies = StackTaxonomy.FamiliesIn(job.Title);
        var titleHasCore = titleFamilies.Overlaps(sig.Core);

        var strengths = new List<string>();
        var risks = new List<string>();
        var missing = new List<string>();

        var tech = ScoreTechnical(profile, sig, jobFamilies, titleFamilies, titleLower, titleHasCore, haystack,
            strengths, risks, missing, out var managerial, out var coreHit);
        var role = ScoreRole(profile, job.Title, titleLower, titleHasCore, strengths, risks);
        var seniority = ScoreSeniority(profile, norm.Seniority, strengths, risks);
        var domain = ScoreDomain(profile, haystack, strengths);
        var location = ScoreLocation(profile, norm.WorkMode, haystack, strengths, risks);
        var language = ScoreLanguage(profile, norm.Language ?? job.Language, risks);

        var overall = (int)Math.Round(
            tech * 0.50 + role * 0.15 + seniority * 0.10 + domain * 0.10 + location * 0.10 + language * 0.05,
            MidpointRounding.AwayFromZero);

        // GATES (the technical fit must dominate; domain/location/seniority must not lift a role
        // that doesn't actually match the candidate's stack into the top).
        if (managerial)
            overall = Math.Min(overall, 40);          // management/business role, not an IC dev job
        if (tech < 35)
            overall = Math.Min(overall, 45);          // no real technical adherence
        if (!coreHit)
            overall = Math.Min(overall, 70);          // stack isn't the candidate's core -> never a top pick

        var recommendation = Recommend(overall);
        var rationale = BuildRationale(overall, tech, role, seniority, domain, location, language);

        return new MatchResult(
            overall, tech, domain, seniority, location, language,
            recommendation, strengths, risks, missing, rationale);
    }

    // Titles that are clearly NOT an individual-contributor dev role…
    private static readonly string[] NonEngRoleTitles =
        { "director", "diretor", " manager", "gerente", "consultant", "consultor", "designer", "marketing",
          "sales", "vendas", "recruiter", "recrutad", "product owner", "product manager", "head of",
          " vp ", "chief", "controller", "accountant", "contador", "advogad", "lawyer", "executive", "analista de neg" };
    // …unless the title also carries a real dev signal.
    private static readonly string[] DevTitleSignals =
        { "developer", "desenvolvedor", "desenvolvedora", "engineer", "engenheir", "programador", "programadora",
          "backend", "back-end", "frontend", "front-end", "fullstack", "full-stack", "software", "qa", "data", "dev " };

    private static int ScoreTechnical(
        CandidateProfile profile, ProfileStackSignature sig, HashSet<StackFamily> jobFamilies,
        HashSet<StackFamily> titleFamilies, string titleLower, bool titleHasCore, string haystack,
        List<string> strengths, List<string> risks, List<string> missing,
        out bool managerial, out bool coreHit)
    {
        managerial = false;
        coreHit = false;

        // Role gate by title: a Director/Manager/Consultant/Marketing role is not for a dev,
        // even when the JD is full of "API/systems/engineering" noise — unless the title also
        // names the candidate's core stack.
        if (ContainsAny(titleLower, NonEngRoleTitles) && !ContainsAny(titleLower, DevTitleSignals) && !titleHasCore)
        {
            managerial = true;
            risks.Add("Cargo não-técnico (gestão/negócio) — não é vaga de dev");
            missing.Add("Não é uma posição de desenvolvedor(a)");
            return 12;
        }

        var coreFamilies = jobFamilies.Where(sig.Core.Contains).ToList();
        var secondaryFamilies = jobFamilies.Where(sig.Secondary.Contains).ToList();
        var excludedFamilies = jobFamilies.Where(sig.Excluded.Contains).ToList();
        // How many of the candidate's own skills (core + secondary) literally appear in the posting.
        var supporting = CountSkillOverlap(profile, haystack);

        // The TITLE is the strongest signal of the role's PRIMARY stack. If the title names a stack
        // family that isn't the candidate's core (e.g. "C# Developer" for a Java profile), the role's
        // primary stack is foreign — a body mention of the core stack is incidental, not a core hit.
        var titleDeclaresForeignCore = sig.Core.Count > 0 && titleFamilies.Count > 0
            && !titleFamilies.Overlaps(sig.Core);

        int score;
        if (coreFamilies.Count > 0 && !titleDeclaresForeignCore)
        {
            coreHit = true;
            score = 72 + Math.Min(supporting * 6, 28);
            strengths.Add($"Stack principal do perfil presente na vaga ({FamilyLabels(coreFamilies)})");
            if (supporting > 2) strengths.Add("Várias skills do perfil citadas na vaga");
        }
        else if (coreFamilies.Count > 0 && titleDeclaresForeignCore)
        {
            // Primary stack of the role differs from the candidate's core; their core appears only in the body.
            score = 50 + Math.Min(supporting * 5, 18);
            risks.Add($"Stack principal da vaga é outra ({FamilyLabels(titleFamilies.ToList())}); seu core aparece só como secundário");
            missing.Add("A vaga não é primariamente da sua stack principal");
        }
        else if (secondaryFamilies.Count > 0)
        {
            score = 48 + Math.Min(supporting * 5, 20);
            risks.Add($"Stack secundária do perfil ({FamilyLabels(secondaryFamilies)}); não é a principal");
            missing.Add("Stack principal do perfil não confirmada na vaga");
        }
        else if (jobFamilies.Count == 0)
        {
            score = 38 + Math.Min(supporting * 5, 20);
            risks.Add("Stack principal da vaga não identificada");
        }
        else
        {
            score = 16 + Math.Min(supporting * 4, 14);
            risks.Add($"Stack da vaga distante do perfil ({FamilyLabels(jobFamilies.ToList())})");
            missing.Add("Aderência técnica baixa ao perfil");
        }

        if (excludedFamilies.Count > 0)
        {
            score = Math.Min(score, 25);
            risks.Add($"Stack excluída pelo perfil ({FamilyLabels(excludedFamilies)})");
        }

        return Clamp(score);
    }

    private static int ScoreRole(
        CandidateProfile profile, string title, string titleLower, bool titleHasCore,
        List<string> strengths, List<string> risks)
    {
        var titleWords = SignificantWords(title);
        int score = 42;

        if (profile.PreferredRoles.Count > 0 && titleWords.Count > 0)
        {
            double best = 0;
            foreach (var role in profile.PreferredRoles)
            {
                var roleWords = SignificantWords(role);
                if (roleWords.Count == 0) continue;
                var shared = roleWords.Count(titleWords.Contains);
                best = Math.Max(best, (double)shared / roleWords.Count);
            }
            score = best >= 0.5 ? 85 : best >= 0.3 ? 68 : best > 0 ? 55 : 42;
        }
        else
        {
            // No declared roles: lean on the title shape.
            score = ContainsAny(titleLower, DevTitleSignals) ? 55 : 42;
        }

        // The title naming the candidate's core stack is a strong role signal on its own.
        if (titleHasCore) score = Math.Max(score, 75);

        if (score >= 75) strengths.Add("Cargo alinhado ao alvo do perfil");
        else if (score < 50) risks.Add("Cargo pouco alinhado aos cargos desejados");
        return score;
    }

    private static int ScoreSeniority(CandidateProfile profile, string? jobSeniority, List<string> strengths, List<string> risks)
    {
        var accepted = AcceptedLevels(profile.Seniority);
        var level = LevelOf(jobSeniority);
        if (level is null) return 65; // not informed

        if (accepted.Contains(level.Value))
        {
            strengths.Add($"Senioridade compatível ({jobSeniority})");
            return 90;
        }
        var distance = accepted.Select(a => Math.Abs(a - level.Value)).Min();
        if (distance == 1) return 68;
        if (distance == 2) { risks.Add($"Senioridade um pouco fora do alvo ({jobSeniority})"); return 50; }
        risks.Add($"Senioridade fora do alvo ({jobSeniority})");
        return 35;
    }

    private static int ScoreDomain(CandidateProfile profile, string haystack, List<string> strengths)
    {
        if (profile.Domains.Count == 0) return 50;
        var hits = profile.Domains
            .Select(d => d.ToLowerInvariant())
            .Where(d => d.Length >= 3 && haystack.Contains(d, StringComparison.Ordinal))
            .Distinct().Count();
        if (hits == 0) return 50; // domain is a bonus, never a gate
        strengths.Add("Domínio do perfil presente na vaga");
        return Clamp(68 + Math.Min(hits * 7, 32));
    }

    private static int ScoreLocation(CandidateProfile profile, string? workMode, string haystack, List<string> strengths, List<string> risks)
    {
        var prefer = profile.PreferredWorkModes.Select(m => m.ToLowerInvariant()).ToHashSet();
        var brazilOrGlobal = haystack.Contains("brazil", StringComparison.Ordinal)
            || haystack.Contains("brasil", StringComparison.Ordinal)
            || haystack.Contains("latam", StringComparison.Ordinal)
            || haystack.Contains("global", StringComparison.Ordinal)
            || haystack.Contains("worldwide", StringComparison.Ordinal);

        switch (workMode)
        {
            case "Remote":
                strengths.Add("Modelo remoto");
                return brazilOrGlobal ? 95 : 88;
            case "Hybrid":
                return prefer.Contains("hybrid") || prefer.Contains("híbrido") || prefer.Contains("hibrido") ? 80 : 62;
            case "Onsite":
                if (prefer.Contains("onsite") || prefer.Contains("presencial")) return 80;
                risks.Add("Modelo presencial; validar localização");
                return 35;
            default:
                return 60;
        }
    }

    private static int ScoreLanguage(CandidateProfile profile, string? language, List<string> risks)
    {
        if (string.IsNullOrEmpty(language)) return 70;
        var prefer = profile.PreferredLanguage;
        if (string.Equals(language, prefer, StringComparison.OrdinalIgnoreCase) || language is "pt-BR" or "en")
            return 90;
        risks.Add($"Idioma da vaga ({language}) pode não ser compatível");
        return 45;
    }

    // ---- helpers ----

    private static int CountSkillOverlap(CandidateProfile profile, string haystack)
    {
        var tokens = profile.CoreSkills.Concat(profile.SecondarySkills)
            .Select(s => s.ToLowerInvariant().Trim())
            .Where(s => s.Length >= 2)
            .Distinct();
        return tokens.Count(t => haystack.Contains(t, StringComparison.Ordinal));
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "da", "do", "the", "and", "com", "para", "pleno", "senior", "sênior", "sr",
        "jr", "junior", "júnior", "mid", "level", "ii", "iii", "para", "em", "of"
    };

    private static HashSet<string> SignificantWords(string text)
    {
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '/', '(', ')', '-', ',', '|', '•', '·', '.', ':', '+' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !StopWords.Contains(w));
        return words.ToHashSet();
    }

    private static readonly Dictionary<string, int> SeniorityLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["intern"] = 1, ["junior"] = 2, ["midlevel"] = 3, ["lead"] = 5, ["staff"] = 6, ["principal"] = 7, ["senior"] = 4
    };

    private static int? LevelOf(string? normalizedSeniority) =>
        normalizedSeniority is not null && SeniorityLevels.TryGetValue(normalizedSeniority, out var lvl) ? lvl : null;

    private static HashSet<int> AcceptedLevels(string profileSeniority)
    {
        var s = (profileSeniority ?? string.Empty).ToLowerInvariant();
        var set = new HashSet<int>();
        if (s.Contains("intern") || s.Contains("estági") || s.Contains("trainee")) set.Add(1);
        if (s.Contains("junior") || s.Contains("júnior") || s.Contains("jr")) set.Add(2);
        if (s.Contains("pleno") || s.Contains("mid")) set.Add(3);
        if (s.Contains("senior") || s.Contains("sênior") || s.Contains("sr")) set.Add(4);
        if (s.Contains("lead") || s.Contains("líder") || s.Contains("lider")) set.Add(5);
        if (s.Contains("staff")) set.Add(6);
        if (s.Contains("principal")) set.Add(7);
        if (set.Count == 0) { set.Add(3); set.Add(4); } // default target: Pleno/Sênior
        return set;
    }

    private static string FamilyLabels(IEnumerable<StackFamily> families) =>
        string.Join(", ", families.Select(f => f.ToString()));

    private static MatchRecommendation Recommend(int overall) => overall switch
    {
        >= 90 => MatchRecommendation.Strategic,
        >= 75 => MatchRecommendation.Prioritize,
        >= 60 => MatchRecommendation.Apply,
        >= 40 => MatchRecommendation.SaveForLater,
        _ => MatchRecommendation.Ignore
    };

    private static string BuildRationale(int overall, int tech, int role, int sen, int dom, int loc, int lang) =>
        $"Score {overall}/100 — Técnico {tech}, Cargo {role}, Senioridade {sen}, " +
        $"Domínio {dom}, Localização {loc}, Idioma {lang}.";

    private static bool ContainsAny(string haystack, string[] tokens) =>
        tokens.Any(t => haystack.Contains(t, StringComparison.Ordinal));

    private static int Clamp(int v) => Math.Clamp(v, 0, 100);
}
