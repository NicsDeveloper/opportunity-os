using OpportunityOS.Application.AI;

namespace OpportunityOS.Infrastructure.Ai;

/// <summary>
/// Deterministic LLM stand-in used when no OpenAI API key is configured. Returns
/// valid JSON per prompt version so the full AI path (parse + persist) works
/// offline. Construct with <c>isConfigured: false</c> to force heuristic fallback
/// (i.e. when LLM analysis is disabled).
/// </summary>
public sealed class FakeLlmProvider : ILlmProvider
{
    public FakeLlmProvider(bool isConfigured = true) => IsConfigured = isConfigured;

    public string ModelName => "fake-llm-v1";
    public bool IsConfigured { get; }

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        var json = request.PromptVersion switch
        {
            Prompts.JobAnalysisVersion => JobAnalysisJson,
            Prompts.FitVersion => FitJson,
            Prompts.OutreachVersion => OutreachJson,
            Prompts.CvTailoringVersion => CvTailoringJson,
            Prompts.CareerInsightVersion => CareerInsightJson,
            _ => "{}"
        };
        return Task.FromResult(LlmResponse.Ok(json, ModelName, request.PromptVersion));
    }

    private const string JobAnalysisJson = """
    {
      "requiredSkills": [".NET", "C#", "ASP.NET Core"],
      "niceToHaveSkills": ["Kafka", "AWS"],
      "domains": ["Payments", "PIX"],
      "seniority": "Senior",
      "workMode": "Remote",
      "language": "en",
      "responsibilities": ["Build and operate payment APIs"],
      "risks": ["Requisitos de idioma a confirmar"],
      "summary": "Vaga backend .NET com foco em pagamentos e PIX."
    }
    """;

    private const string FitJson = """
    {
      "technicalScore": 90, "domainScore": 92, "seniorityScore": 90,
      "locationScore": 90, "languageScore": 85, "overallScore": 89,
      "recommendation": "Prioritize",
      "rationale": "Forte aderência técnica e de domínio (pagamentos/.NET).",
      "strengths": ["Stack .NET", "Domínio de pagamentos"],
      "risks": ["Confirmar nível de inglês"],
      "missingRequirements": []
    }
    """;

    private const string OutreachJson = """
    {
      "linkedInMessage": "Olá! Vi a vaga e meu perfil tem forte aderência em backend .NET e pagamentos. Podemos conversar?",
      "coverLetter": "Tenho interesse na posição. Atuo com backend .NET em sistemas financeiros e pagamentos...",
      "emailSubject": "Candidatura — Backend .NET",
      "emailBody": "Olá, tenho interesse na vaga de backend .NET com foco em pagamentos...",
      "cvTailoringNotes": "Destacar .NET, pagamentos e mensageria.",
      "followUpMessage": "Olá! Reforçando meu interesse na posição. Houve chance de avaliar meu perfil?",
      "humanReviewNotes": "Revise e personalize antes de enviar."
    }
    """;

    private const string CvTailoringJson = """
    {
      "summaryAdjustment": "Enfatizar pagamentos e .NET no resumo.",
      "skillsToHighlight": [".NET", "C#", "Kafka"],
      "keywordsToInclude": ["PIX", "Open Finance"],
      "bulletSuggestions": ["Destacar projeto de pagamentos com .NET e Kafka."],
      "sectionsToReorder": ["Experiência financeira no topo."],
      "notes": "Ajustar apenas o que é verdadeiro."
    }
    """;

    private const string CareerInsightJson = """
    {
      "mostRequestedTechnologies": [".NET", "Kafka", "AWS"],
      "recurringGaps": ["Kubernetes avançado"],
      "strongestDomains": ["Payments", "PIX"],
      "studySuggestions": ["Aprofundar em Kubernetes."],
      "postIdeas": ["Como modelar idempotência em pagamentos."],
      "mostPromisingCompanies": ["Fintechs com Greenhouse/Lever"],
      "summary": "Mercado pede .NET + mensageria + cloud em fintechs."
    }
    """;
}
