using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.AI;

/// <summary>
/// Versioned prompt templates for the AI Copilot layer. Bumping a prompt means
/// bumping its version constant so executions stay auditable (PromptExecutionLog).
/// </summary>
public static class Prompts
{
    public const string JobAnalysisVersion = "job-analysis-v1";
    public const string FitVersion = "fit-score-v2";
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
            "Você é um avaliador de compatibilidade entre candidato e vaga. " +
            "PRIORIDADE: aderência técnica às CORE SKILLS DO PERFIL (campo coreSkills do JSON do perfil), " +
            "sejam elas quais forem — para um perfil .NET isso é .NET/C#/ASP.NET Core; para um perfil Java é " +
            "Java/Spring; para Frontend é React/TypeScript; para Data Engineer é Python/Airflow/Spark; etc. " +
            "Uma vaga claramente alinhada às core skills do perfil é um match FORTE (technicalScore >= 80 e " +
            "overallScore >= 75). Domínios (ex.: pagamentos/PIX/Open Finance/fintech) são BÔNUS quando aderentes " +
            "ao perfil, NUNCA pré-requisito nem gate. Penalize de verdade só quando a stack principal da vaga é " +
            "DISTANTE das core skills do perfil (ex.: vaga claramente de outra linguagem/área, ou estágio); " +
            "respeite o campo excludedStacks do perfil. NÃO assuma .NET como referência global — use sempre o perfil. " +
            "Trate Pleno e Sênior como igualmente adequados quando o perfil busca ambos; não penalize Pleno nesse caso. " +
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
