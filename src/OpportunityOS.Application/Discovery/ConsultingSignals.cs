namespace OpportunityOS.Application.Discovery;

/// <summary>
/// Scores how likely a company is an IT consultancy / outsourcing / software house, from
/// explainable signals (Priority 3). Pure/static so the score is transparent and testable.
/// </summary>
public static class ConsultingSignals
{
    public static (int Score, List<string> Signals) Evaluate(string text, bool hasWebsite, bool multipleSources)
    {
        var t = (text ?? string.Empty).ToLowerInvariant();
        var signals = new List<string>();
        int score = 0;

        void Add(int pts, string label) { score += pts; signals.Add($"{(pts >= 0 ? "+" : "")}{pts} {label}"); }
        bool Has(params string[] ns) => ns.Any(n => t.Contains(n));

        if (Has("consultoria", "consulting")) Add(30, "consultoria");
        if (Has("outsourcing", "alocação", "alocacao", "body shop", "bodyshop", "squad")) Add(25, "outsourcing/alocação/squad");
        if (Has("transformação digital", "transformacao digital", "digital transformation")) Add(20, "transformação digital");
        if (Has("carreira", "carreiras", "trabalhe conosco", "careers", "vagas")) Add(20, "página de carreiras");
        if (Has(".net", "c#", "csharp")) Add(15, "vagas .NET/C#");
        if (multipleSources) Add(15, "múltiplas fontes");
        if (Has("clientes", "setores", "cases", "portfólio", "portfolio")) Add(10, "clientes/setores");
        if (Has("nearshore", "software development", "software house", "fábrica de software", "fabrica de software")) Add(10, "nearshore/software dev");

        if (Has("saas", "plataforma", "produto") && !Has("consultoria", "consulting", "outsourcing")) Add(-30, "parece produto/SaaS puro");
        if (Has("marketing", "agência", "agencia") && !Has("ti", "tech", "software", "desenvolv")) Add(-20, "parece agência de marketing");
        if (!hasWebsite) Add(-20, "sem site oficial");

        return (Math.Clamp(score, 0, 100), signals);
    }
}
