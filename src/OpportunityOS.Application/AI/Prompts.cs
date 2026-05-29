using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

/// <summary>
/// Versioned prompt templates for the AI Copilot layer. Bumping a prompt means
/// bumping its version constant so executions stay auditable (PromptExecutionLog).
/// </summary>
public static class Prompts
{
    public const string JobAnalysisVersion = "job-analysis-v1";
    public const string FitVersion = "fit-score-v1";
    public const string OutreachVersion = "outreach-v1";
    public const string CvTailoringVersion = "cv-tailoring-v1";
    public const string CareerInsightVersion = "career-insight-v1";

    private const string NoInvention =
        "Regras invioláveis: não invente experiências do candidato; use apenas fatos do perfil e da vaga; " +
        "responda SOMENTE com JSON válido, sem texto fora do JSON.";

    public static LlmRequest JobAnalysis(JobPosting job) => new(
        SystemPrompt:
            "Você é um analista técnico de vagas de engenharia de software. " + NoInvention,
        UserPrompt:
            $$"""
            Analise a vaga e retorne JSON com EXATAMENTE estas chaves:
            {
              "requiredSkills": [], "niceToHaveSkills": [], "domains": [],
              "seniority": "", "workMode": "", "language": "",
              "responsibilities": [], "risks": [], "summary": ""
            }

            Vaga:
            {{AiJson.SerializeJob(job)}}
            """,
        PromptVersion: JobAnalysisVersion);

    public static LlmRequest Fit(CandidateProfile profile, JobAnalysisResult analysis) => new(
        SystemPrompt:
            "Você é um avaliador de compatibilidade entre candidato e vaga. Penalize stacks distantes; " +
            "valorize pagamentos, PIX, Open Finance, fintech, banking, mensageria, cloud e .NET. " +
            "Trate Pleno e Sênior como igualmente adequados (o candidato busca ambos); não penalize vagas Pleno. " +
            "Escreva 'rationale', 'strengths', 'risks' e 'missingRequirements' dirigindo-se ao candidato em " +
            "SEGUNDA PESSOA ('você', 'seu'); nunca se refira ao candidato pelo nome ou em terceira pessoa. " + NoInvention,
        UserPrompt:
            $$"""
            Retorne JSON com EXATAMENTE estas chaves (scores 0-100, recommendation um de
            Ignore|SaveForLater|Apply|Prioritize|Strategic):
            {
              "technicalScore":0,"domainScore":0,"seniorityScore":0,"locationScore":0,
              "languageScore":0,"overallScore":0,"recommendation":"",
              "rationale":"","strengths":[],"risks":[],"missingRequirements":[]
            }

            Perfil:
            {{AiJson.SerializeProfile(profile)}}

            Vaga analisada:
            {{AiJson.SerializeAnalysis(analysis)}}
            """,
        PromptVersion: FitVersion);

    public static LlmRequest Outreach(CandidateProfile profile, JobPosting job, OpportunityMatch match) => new(
        SystemPrompt:
            "Você é especialista em abordagem profissional para recrutadores de tecnologia. " +
            "Mensagens curtas, humanas, sem bajulação, sem inventar conexão pessoal. " +
            "Escreva em português, exceto se a vaga estiver em inglês. " + NoInvention,
        UserPrompt:
            $$"""
            Gere drafts para REVISÃO HUMANA (nada é enviado automaticamente). Retorne JSON:
            {
              "linkedInMessage": "", "coverLetter": "", "emailSubject": "", "emailBody": "",
              "cvTailoringNotes": "", "followUpMessage": "", "humanReviewNotes": ""
            }
            Limites: linkedInMessage <= 700 caracteres; coverLetter até 4 parágrafos.

            Perfil:
            {{AiJson.SerializeProfile(profile)}}

            Vaga:
            {{AiJson.SerializeJob(job)}}

            Análise de compatibilidade:
            {{AiJson.SerializeMatch(match)}}
            """,
        PromptVersion: OutreachVersion);

    public static LlmRequest CvTailoring(CandidateProfile profile, JobPosting job, OpportunityMatch match) => new(
        SystemPrompt:
            "Você sugere ajustes de CV para uma vaga específica. NÃO altere o CV; apenas recomende. " + NoInvention,
        UserPrompt:
            $$"""
            Retorne JSON:
            {
              "summaryAdjustment": "", "skillsToHighlight": [], "keywordsToInclude": [],
              "bulletSuggestions": [], "sectionsToReorder": [], "notes": ""
            }

            Perfil:
            {{AiJson.SerializeProfile(profile)}}

            Vaga:
            {{AiJson.SerializeJob(job)}}

            Análise:
            {{AiJson.SerializeMatch(match)}}
            """,
        PromptVersion: CvTailoringVersion);

    public static LlmRequest CareerInsights(CandidateProfile profile, IEnumerable<JobPosting> jobs) => new(
        SystemPrompt:
            "Você é um estrategista de carreira técnica. Seja direto e baseado em dados. " + NoInvention,
        UserPrompt:
            $$"""
            Analise os padrões nas vagas e retorne JSON:
            {
              "mostRequestedTechnologies": [], "recurringGaps": [], "strongestDomains": [],
              "studySuggestions": [], "postIdeas": [], "mostPromisingCompanies": [], "summary": ""
            }

            Perfil:
            {{AiJson.SerializeProfile(profile)}}

            Vagas analisadas:
            [{{string.Join(",", jobs.Select(AiJson.SerializeJob))}}]
            """,
        PromptVersion: CareerInsightVersion);
}
