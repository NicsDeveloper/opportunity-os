using System.Globalization;
using System.Text;

namespace OpportunityOS.Application.Discovery;

/// <summary>One job the user marked as irrelevant/hide — the raw signal we learn from.</summary>
public sealed record DislikedJob(Guid CompanyId, IEnumerable<string> Skills, string? Title, string? Reason);

/// <summary>A candidate job to score against what the user has rejected before.</summary>
public sealed record JobSignal(Guid CompanyId, IReadOnlyCollection<string> Skills, string? Title, string? Description);

/// <summary>
/// What the system has learned from rejections: how often each company/skill/word-from-reason
/// appeared in jobs the user threw away. Used to penalize (and hide) similar future jobs.
/// </summary>
public sealed record NegativeModel(
    IReadOnlyDictionary<Guid, int> Companies,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyDictionary<string, int> ReasonTokens)
{
    public static readonly NegativeModel Empty =
        new(new Dictionary<Guid, int>(), new Dictionary<string, int>(), new Dictionary<string, int>());

    public int TotalRejections { get; init; }
}

/// <summary>
/// Continuous-improvement loop (heuristic, no LLM): builds a negative-preference model from the
/// user's "irrelevante"/"ocultar" feedback (and the reasons they type) and scores new jobs so the
/// system stops surfacing roles like the ones already rejected. Pure and stateless.
/// </summary>
public interface IFeedbackLearningService
{
    NegativeModel Build(IReadOnlyCollection<DislikedJob> disliked);
    int Penalty(NegativeModel model, JobSignal job);
    bool ShouldHide(NegativeModel model, JobSignal job);
}

public sealed class FeedbackLearningService : IFeedbackLearningService
{
    // Weights: how much one past rejection of the same company / skill / reason-word costs a new job.
    private const int CompanyWeight = 14;
    private const int TokenWeight = 5;

    // Hard-hide gates: a company rejected this many times, or an overwhelming penalty, disappears.
    private const int CompanyHideCount = 3;
    private const int HidePenalty = 50;

    // Words that must NEVER become a negative signal: stop-words PLUS common role/stack/location terms
    // that appear in almost every good .NET posting. "senior demais" means *this* role is too senior —
    // it does NOT mean "senior is bad", so penalizing every senior role would nuke the whole board.
    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        // generic stop-words
        "a","o","as","os","de","da","do","das","dos","e","ou","que","com","sem","para","pra","por","em",
        "no","na","nos","nas","um","uma","uns","umas","ao","aos","é","ser","muito","muita","mais","menos",
        "nao","não","sim","esse","essa","este","esta","isso","vaga","vagas","empresa","cargo","the","and",
        "for","with","this","that","job","role","demais","muitos","muitas","ainda","tem","aqui",
        // role / seniority (desirable, NOT negatives)
        "senior","sênior","junior","júnior","pleno","jr","sr","estagio","estágio","trainee",
        "desenvolvedor","desenvolvedora","developer","engenheiro","engenheira","engineer","programador",
        "programadora","analista","arquiteto","arquiteta","especialista","tech","lead","dev","software",
        "sistemas","sistema","desenvolvimento","programacao","programação","ti",
        // stack (desirable)
        "net",".net","c#","csharp","dotnet","backend","back","frontend","front","fullstack","full","stack",
        "api","apis","azure","aws","cloud","sql","java","python","node","react","angular","kafka","docker",
        // location / contract that appear in GOOD jobs (dangerous to learn). "presencial"/"internacional"
        // are intentionally NOT here — those are valid negatives the user may want to learn from.
        "remoto","remote","clt","pj","brasil","brazil","sao","são","paulo","rio",
    };

    public NegativeModel Build(IReadOnlyCollection<DislikedJob> disliked)
    {
        if (disliked is null || disliked.Count == 0) return NegativeModel.Empty;

        var companies = new Dictionary<Guid, int>();
        var tokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in disliked)
        {
            if (d.CompanyId != Guid.Empty)
                companies[d.CompanyId] = companies.GetValueOrDefault(d.CompanyId) + 1;

            // Reason words are the explicit, highest-signal part — that's why the user can type a motive.
            foreach (var t in Tokenize(d.Reason))
                tokens[t] = tokens.GetValueOrDefault(t) + 1;
        }

        return new NegativeModel(companies, new Dictionary<string, int>(), tokens) { TotalRejections = disliked.Count };
    }

    public int Penalty(NegativeModel model, JobSignal job)
    {
        if (model is null || model.TotalRejections == 0) return 0;
        var penalty = 0;

        // Company the user keeps rejecting -> clear, safe signal.
        if (model.Companies.TryGetValue(job.CompanyId, out var c)) penalty += c * CompanyWeight;

        // ONLY the words the user explicitly typed as a reason. We deliberately do NOT learn the
        // job's own skills: a .NET role rejected for being onsite must never teach ".NET is bad".
        if (model.ReasonTokens.Count > 0)
        {
            var text = Norm($"{job.Title} {job.Description}");
            foreach (var (tok, n) in model.ReasonTokens)
                if (text.Contains(tok, StringComparison.Ordinal)) penalty += n * TokenWeight;
        }

        return penalty;
    }

    public bool ShouldHide(NegativeModel model, JobSignal job)
    {
        if (model is null || model.TotalRejections == 0) return false;
        // A company the user keeps rejecting is a clear "stop showing me this" signal.
        if (model.Companies.TryGetValue(job.CompanyId, out var c) && c >= CompanyHideCount) return true;
        return Penalty(model, job) >= HidePenalty;
    }

    private static IEnumerable<string> Tokenize(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) yield break;
        foreach (var raw in Norm(reason).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (raw.Length >= 3 && !Stop.Contains(raw)) yield return raw;
    }

    /// <summary>Lowercase + strip accents so "júnior"/"junior"/"PHP " all match.</summary>
    private static string Norm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var lowered = s.Trim().ToLowerInvariant();
        var sb = new StringBuilder(lowered.Length);
        foreach (var ch in lowered.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) || ch == ' ' || ch == '#' || ch == '+' || ch == '.' ? ch : ' ');
        }
        return sb.ToString();
    }
}
