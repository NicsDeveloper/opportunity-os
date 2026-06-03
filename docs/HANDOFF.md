# Opportunity OS — Mapa completo do sistema (handoff)

> Documento de transferência para outro agente/dev. Descreve **tudo** que o sistema é,
> fornece e faz, e o **fluxo lógico** de ponta a ponta. Escrito a partir do código real
> (não da intenção). Última varredura: branch `feat/opportunity-os`.

---

## 1. O que é

Sistema pessoal de **inteligência de oportunidades de emprego** para um candidato backend
**.NET/C#** (Nícolas Serrano — Pleno/Sênior, fintech/pagamentos, remoto, inglês B2). Ele:

1. **Descobre** vagas continuamente (ATS públicos + sites de carreira + busca web-aberta).
2. **Pontua** cada vaga contra o perfil do candidato (heurístico + LLM).
3. **Mostra** numa **única tela viva** as melhores oportunidades, frescas e relevantes.
4. **Gera** rascunhos de mensagem/e-mail/follow-up (LLM) — sempre para **revisão humana**.
5. **Acompanha** num pipeline (oportunidades, follow-ups) e pode mandar **digest por e-mail**.

**Princípios invioláveis (do produto):**
- Nada é enviado automaticamente. Toda comunicação externa é rascunho para revisão humana.
- Sem automação de LinkedIn. Sem submissão automática de candidatura.
- Chaves/segredos nunca no repositório (só `dotnet user-secrets`).
- Relevância **+ frescor** primeiro: vaga obsoleta não aparece no topo.

---

## 2. Arquitetura e stack

**Clean Architecture** em .NET 10, 6 projetos:

| Projeto | Papel | Depende de |
|---|---|---|
| `OpportunityOS.Domain` | Entidades + enums (regras de domínio, sem deps) | — |
| `OpportunityOS.Contracts` | DTOs de request/response da API | Domain |
| `OpportunityOS.Application` | Casos de uso, interfaces (ports), serviços de domínio | Domain, Contracts |
| `OpportunityOS.Infrastructure` | EF Core, providers HTTP, LLM, e-mail, Playwright | Application |
| `OpportunityOS.Api` | Minimal API (endpoints), seed, migrations no startup | Infrastructure |
| `OpportunityOS.Worker` | Hangfire (jobs recorrentes) | Infrastructure |

**Tech:** .NET 10 · EF Core 10 + **PostgreSQL 18** (Docker) · Minimal API + OpenAPI ·
Hangfire (storage PostgreSQL) · Microsoft.Playwright 1.49 (Chromium headless) ·
React + Vite + TypeScript (frontend) · xUnit (136 testes).

**Portas/processos:** API em `http://localhost:5077` · Worker é processo separado ·
frontend Vite em `:5173` com **proxy `/api` → `:5077`** (não há CORS no servidor).
A API roda **migrations + seed no startup** (`SeedOnStartup=true`).

---

## 3. Modelo de domínio (entidades)

Todas com `Id` Guid e setters privados (encapsulamento). Tabelas snake_case; listas
`List<string>` viram **jsonb** via value converters.

| Entidade | Tabela | Núcleo | Métodos de domínio |
|---|---|---|---|
| `CandidateProfile` | candidate_profiles | FullName, Headline, Summary, Location, Seniority, PreferredLanguage, CoreSkills, SecondarySkills, Domains, PreferredRoles/ContractTypes/Locations, Experiences (jsonb) | `Update(...)` |
| `Company` | companies | Name, WebsiteUrl, CareersUrl, LinkedInUrl, Industry, Country, Priority, Source, Tags | `Update`, `MarkScanned`, `SetCareersUrl`, `SetWebsiteUrl`, `AddTag`, `RaisePriorityTo` (sobe sem rebaixar), `MarkSourceOnly` (rebaixa denylist a Low + source-only/noisy-source/do-not-promote) |
| `JobPosting` | job_postings | CompanyId, ExternalId, **SourceProvider**, Title, Location, WorkMode, Seniority, Language, AbsoluteUrl, DescriptionText/Html, ExtractedSkills/Domains, **Status**, PublishedAtUtc, CreatedAtUtc, UpdatedAtUtc | `RefreshFromSource`, `ApplyNormalization`, `MarkAnalyzed`, `Archive`, `MarkExpired` |
| `OpportunityMatch` | opportunity_matches | JobPostingId, CandidateProfileId, OverallScore + 5 subscores, Recommendation, Strengths/Risks/MissingRequirements, **Rationale** | (imutável após criação) |
| `Opportunity` | opportunities | JobPostingId (único), RecruiterLeadId?, **Status**, NextFollowUpAtUtc, Notes | `AdvanceTo`, `SetStatusManually`, `SetNotes`, `SetFollowUp`, `LinkRecruiter`, `Create` |
| `GeneratedMessage` | generated_messages | JobPostingId, OpportunityMatchId, LinkedInMessage, CoverLetter, EmailSubject/Body, CvTailoringNotes, FollowUpMessage, HumanReviewNotes, Status, PromptVersion, ModelName | `SetStatus` |
| `ExecutionRun` | execution_runs | RunType, Status, Started/Finished, ItemsProcessed/Succeeded/Failed, ErrorMessage | `RecordSuccess`, `RecordFailure`, `Complete`, `Fail`, `Start` |
| `PromptExecutionLog` | prompt_execution_logs | Service, PromptVersion, ModelName, Success, UsedFallback, RawResponse, JobPostingId? | auditoria de cada chamada LLM |
| `RecruiterLead` | recruiter_leads | CompanyId, FullName, Source, … | CRUD |
| `BacenInstitution` | bacen_institutions | Name, Ispb, Cnpj, InstitutionType, AuthorizedByBacen, Pix/Spi fields, Tags | `UpdateFrom` (staging do radar Bacen) |
| `RawJobCandidate` | raw_job_candidates | Title, Snippet, DiscoveredUrl, SourceProvider/Name, **SourceType**, RealCompanyName, OriginalJobUrl, Location/WorkMode/Language, **Status** (RawJobCandidateStatus), SourceConfidenceScore, PreliminaryFitScore, **NormalizedFingerprint**, RequiresManualValidation, **VerificationStatus**, SearchCampaignId, Query, PromotedJobPostingId | `Classify`, `Enrich`, `SetVerification`, `MarkDuplicate`, `PromoteTo`, `Reject` — **núcleo do Firehose** (§4.1) |
| `SearchCampaign` | search_campaigns | Name, Description, **Status**/**Priority**, BaseKeywords, DailyQueryBudget, LastRunAtUtc | conjuntos de busca salvos (rodáveis) |
| `SearchQueryExecution` | search_query_executions | SearchCampaignId, Query, **Status**, ResultsCount, StartedAt | auditoria de cada query do Firehose |
| `JobPostingSourceOccurrence` | job_posting_source_occurrences | JobPostingId, SourceProvider/Name, Url, SeenAtUtc | cada vez que a MESMA vaga aparece em outra fonte (dedup por fingerprint) |
| `UserFeedback` | user_feedbacks | **Type** (UserFeedbackType), JobPostingId?, RawJobCandidateId?, Reason | feedback do usuário (relevante/ocultar/aplicada/…) — alimenta DiscoveryRank e o mural de aplicações |
| `ConsultingCompanyCandidate` | consulting_company_candidates | Name, WebsiteUrl, Country, Source, Signals, **ConsultingConfidenceScore**, **Status** | candidatas do radar de consultorias (antes de virar `Company`) |

`JobPosting` tem propriedades calculadas (não-mapeadas): **`EffectiveDateUtc` = PublishedAtUtc ?? CreatedAtUtc** e **`IsTalentPool`** (título com "banco de talentos"/"talent pool"/"cadastro de currículo"). Além do núcleo, carrega **qualidade de fonte** (do Firehose): `SourceType`, `SourceName`, `SourceConfidenceScore`, `RequiresManualValidation`, `RealCompanyName`, `OriginalJobUrl`, `NormalizedFingerprint` (`SetSourceQuality`/`RefreshFromSource`).

### Enums e máquinas de estado
- `CompanyPriority`: Low(1) → Medium → High → Strategic(4)
- `CompanySource`: Manual, CsvImport, SearchEngine, PublicRegistry, AtsDiscovery, Bacen, **SearchDiscovery** (usado pelo seed manual `ObservedCompaniesSeed`)
- `JobPostingStatus`: Discovered → Normalized → Analyzed → Shortlisted/Rejected/Archived/Expired
- `MatchRecommendation`: Ignore → SaveForLater → Apply → Prioritize → Strategic
- `OpportunityStatus`: Discovered → Analyzed → MessageGenerated → **ReadyForHumanReview** (teto do sistema) → SentManually → AppliedManually → WaitingResponse → InterviewScheduled → Rejected/Archived
- `GeneratedMessageStatus`: Draft → Reviewed → Used → Discarded
- `ExecutionRunStatus`: Running → Succeeded/Failed/PartiallyFailed
- **Firehose (§4.1):** `SourceType` (OfficialAts/OfficialCareerPage/JobBoard/Aggregator/SearchResult/SocialIndexed/Unknown) · `RawJobCandidateStatus` (Discovered→Classified→Enriched→Duplicate/PromotedToJobPosting/Rejected/Expired) · `JobVerificationStatus` (Unverified→AggregatorOnly→LikelyOriginal→VerifiedOriginal/OfficialAts/Expired) · `SearchCampaignStatus`/`SearchCampaignPriority` · `ConsultingCompanyCandidateStatus` (Candidate→PromotedToCompany/Rejected/Duplicate) · `UserFeedbackType` (Relevant/Irrelevant/HideSimilar/BadCompanyDetection/BadScore/Duplicate/Expired/InterestingCompany/Applied/ContactedRecruiter)

---

## 4. Descoberta de vagas (providers)

Duas abstrações principais (ports em `Application/Discovery`):

### `IJobSourceProvider` — descoberta **por empresa** (`DiscoverJobsAsync(company)`)
Para empresas que já têm `CareersUrl`/board conhecido.
- **GreenhouseJobSourceProvider** — `boards-api.greenhouse.io/v1/boards/{token}/jobs?content=true`
- **LeverJobSourceProvider** — `api.lever.co/v0/postings/{handle}?mode=json`
- **SmartRecruitersJobSourceProvider** — `api.smartrecruiters.com/v1/companies/{id}/postings`
- **AshbyJobSourceProvider** — `api.ashbyhq.com/posting-api/job-board/{board}`
- **GenericCareersCrawler** — acessa o **site da empresa**, acha "Carreiras/Trabalhe Conosco", extrai links de vaga por regex de cargo; exclui hosts de ATS; mesma origem; `ExternalId` = hash da URL. **Fallback Playwright** para páginas SPA (render JS) quando o HTML estático não traz vagas.

### `IJobSearchProvider` — busca **por palavra-chave, cross-company** (`SearchAsync(keywords)`)
Cada resultado vira uma oportunidade; empresa derivada do host; cria empresa se necessário.
- **GupyJobSearchProvider** — `portal.api.gupy.io/api/v1/jobs` (portal público de vagas Gupy).
- **SerperWebJobSearchProvider** — **busca web-aberta real** via **Serper.dev** (`google.serper.dev/search`, índice Google). Deriva empresa do host, filtra agregadores (LinkedIn/Indeed/Glassdoor/ZipRecruiter/bebee/catho/…), frescor via `tbs=qdr:m`. Respeita rate-limit do tier grátis (`SerperDelayMs` entre queries) e teto `SerperMaxQueriesPerCall`. **Requer `Search:SerperApiKey`** (senão dormente).
- **GoogleWebJobSearchProvider** — busca CSE Google. **Dormente/descontinuado para web-aberta**: a JSON API do Google não serve mais engines whole-web (403 desde jan/2026). Só funciona com `cx` escopado a sites. Mantido no código atrás de `Search:ApiKey`+`Search:SearchEngineId`.

### Provedores de apoio à descoberta
- **`IAtsDetector` (AtsDetector)** — reconhece o ATS de uma empresa pela URL/HTML: Greenhouse, Lever, Gupy, Workday, Ashby, SmartRecruiters, Workable, Recruitee, Teamtailor, Breezy, inhire, Abler, Solides, Pandapé, Kenoby, Quickin, JobConvo, Taqe, 99jobs, Recrutei, GeekHunter, Coodesh, Programathor.
- **`IAtsBoardFinder`** — acha o board de vagas de uma empresa. `GoogleAtsBoardFinder` (se CSE configurado) → fallback `HeuristicAtsBoardFinder`.
- **`ICompanyWebsiteDiscoverer`** — descobre o site oficial pelo nome. `GoogleWebsiteDiscoverer` (se CSE) → fallback `CompanyWebsiteDiscoverer` (heurística de domínios).
- **`IJobContentEnricher` (HtmlJobContentEnricher)** — baixa o **texto real da página da vaga** (HTTP GET + strip de tags; fallback Playwright para SPA; trunca 6k) antes de pontuar, para snippets curtos não subestimarem boas vagas.
- **`IJobLinkValidator` (JobLinkValidator)** — HEAD-check; marca 404/410 como `Expired` (some do feed).
- **`IPageRenderer` (PlaywrightPageRenderer)** — Chromium headless compartilhado; degrada para HTTP se o browser não estiver instalado.
- **`GoogleQuotaGuard`** — cota diária compartilhada entre todos os chamadores do Google CSE (default 90/100, reset UTC).

### Orquestração — `JobDiscoveryService`
- `DiscoverAsync(companyId?)` → varre empresas (por prioridade) com os `IJobSourceProvider` que sabem lidar com cada uma.
- `SearchAsync(keywords)` → roda todos os `IJobSearchProvider` (Gupy + Serper + Google).
- **Resiliente**: falha de um provider/empresa é logada e registrada no `ExecutionRun`, sem abortar o ciclo.
- **Dedup** por `(SourceProvider, ExternalId)` — vaga reencontrada é **atualizada, nunca duplicada** (índice único no banco).
- **Enriquecimento**: descrição curta (<300 chars) → busca texto real da página (teto 8/run).
- **Auto-score na descoberta** (o que faz a vaga **aparecer na tela** sem clique):
  - Todo job novo é pontuado pelo **HeuristicMatchEngine** → cria `OpportunityMatch`.
  - Se heurístico **≥60** e há orçamento (**teto 6 análises LLM/run**), faz **upgrade para score LLM** (understanding + fit) na hora — número autoritativo "de bate pronto".
  - Jobs antigos sem match são pontuados quando reencontrados (backfill).
  - Falha de score/LLM nunca interrompe a descoberta (fallback heurístico).

### 4.1 Firehose (descoberta massiva via busca, com triagem)

Pipeline paralelo ao §4 que captura **muito** da web aberta como `RawJobCandidate` (zona de
triagem), só promovendo a `JobPosting` o que passa pelos filtros. Alimenta a aba **"Explorar tudo"**.

- **`IFirehoseService`** — roda `SearchCampaign`s (buscas salvas), `quick-search` e `aggressive-search`.
  Cada resultado vira um `RawJobCandidate` (não um JobPosting ainda). Registra `SearchQueryExecution`.
- **`SourceClassifierService`** — classifica a fonte (`SourceType`) e a confiança (0–100) pela URL/título:
  ATS oficial > página de carreira > job board > agregador > resultado de busca > social. Hosts de ATS
  conhecidos (Greenhouse/Lever/Gupy/Ashby/Quickin/Solides/Kenoby/JobConvo/99jobs/Abler/Pandapé/inhire/…)
  ganham confiança alta; agregadores ganham `RequiresManualValidation`.
- **`CompanyNameResolver`** — tenta extrair a **empresa real** do título/host (com denylist de rótulos
  genéricos e hosts path-slug como quickin.io). Sem empresa confiável, fica "a confirmar".
- **`JobFingerprintService`** — fingerprint normalizado (empresa+cargo+local) para **dedup**: a mesma vaga
  vista em várias fontes vira `JobPostingSourceOccurrence`, não duplicata.
- **`RawCandidatePromotionService`** — promove candidato → `JobPosting` + `OpportunityMatch` quando há
  empresa resolvida e qualidade mínima; `resolve-original` tenta achar a vaga no ATS oficial antes.
- **`QueryBudgetManager`** — tetos diários por centro de custo (queries Serper/CSE e análises LLM).
- **`DiscoveryRankService`** — rank do feed = **FitScore×0.50 + SourceConfidence×0.20 + Freshness×0.15
  + CompanyPriority×0.10 + FeedbackBoost×0.05** (puro/stateless). É o `sort=rank` da aba "Boas opções".
- **`IConsultingRadarService`** — radar de consultorias .NET: descobre candidatas (`ConsultingCompanyCandidate`),
  pontua confiança e promove a `Company` (manual ou em lote) — não cria vaga.
- **`IFeedbackLearningService` (melhoria contínua, sem LLM)** — aprende dos descartes: monta um
  *modelo negativo* a partir de TODO feedback `Irrelevant`/`HideSimilar` (empresa, stack e as **palavras
  do motivo** que o usuário digita ao dispensar). No `/api/matches` aplica **penalidade** no ranking de
  vagas parecidas e **oculta** quando o sinal é forte (mesma empresa rejeitada ≥3× ou penalidade alta).
  Acento-insensível. Quanto mais o usuário marca "não serve" + motivo, menos vagas do tipo aparecem.

---

## 5. Match engine (relevância)

### `HeuristicMatchEngine` (sem LLM, transparente)
`OverallScore = Técnico*0.45 + Domínio*0.20 + Senioridade*0.15 + Localização*0.10 + Idioma*0.10`

- **Técnico**: .NET/C# explícito = base 65 + bônus de stack (cloud/mensageria/dados), -penalidade por linguagens concorrentes; backend sem .NET = ~48; nem backend = baixo. *Técnico domina — .NET é match forte mesmo fora de pagamentos.*
- **Domínio**: financeiro/pagamentos = 82+; adjacente = 60+; **sem sinal = 50 (bônus, não gate)**.
- **Senioridade**: Mid/Senior = 90; Lead = 70; Staff/Principal = 50; Junior/Intern = 30; não informado = 65.
- **Localização**: Remote = 85–95; Hybrid = 65; Onsite = 35; desconhecido = 60.
- **Idioma**: pt-BR/en = 90; outro = 40.
- **Recomendação**: ≥90 Strategic · ≥75 Prioritize · ≥60 Apply · ≥40 SaveForLater · senão Ignore.
- Rationale heurístico é seco: `"Score 68/100 — Técnico 65, Domínio 50, …"`.

`JobNormalizer` (+ `KnownTerms`) deriva Seniority, WorkMode, Language, Skills, Domains do título/descrição.

---

## 6. Camada de IA (AI Copilot)

`ILlmProvider` selecionado em runtime (`Llm:Provider` = auto|anthropic|openai|fake):
- **AnthropicLlmProvider** (claude-sonnet-4-6, default se há `Anthropic:ApiKey`)
- **OpenAiLlmProvider** (gpt-4.1-mini)
- **FakeLlmProvider** (determinístico, JSON válido — usado se sem chave ou `EnableLlmAnalysis=false`)

Cinco serviços (cada um com prompt versionado e auditado em `PromptExecutionLog`):
1. **IJobUnderstandingService** (`job-analysis-v1`) — interpreta a vaga → skills, domínios, senioridade, work mode, idioma, responsabilidades, riscos, resumo.
2. **ICandidateFitAnalysisService** (`fit-score-v1`) — compara perfil × vaga → `OpportunityMatch` com score + rationale rico. *Técnico (.NET/C#/backend) é prioridade; pagamentos é bônus, não gate; .NET role ⇒ match forte (≥75).*
3. **IOutreachDraftService** (`outreach-v1`) — rascunhos: mensagem LinkedIn/direta, cover letter, assunto+corpo de e-mail, follow-up, observações de revisão humana.
4. **ICvTailoringSuggestionService** (`cv-tailoring-v1`) — sugestões de ajuste de CV (advisory).
5. **ICareerInsightService** (`career-insight-v1`) — padrões entre várias vagas analisadas.

Gate: **outreach exige score ≥60** (`MinScoreForOutreach`).

---

## 7. Pipeline de oportunidades

`OpportunityPipeline`:
- **Auto-cria** `Opportunity` quando um match tem score **≥70** (`AutoCreateThreshold`), status inicial `Analyzed`.
- Ao gerar mensagem, avança a oportunidade — mas o **sistema só pode chegar até `ReadyForHumanReview`**. Daí pra frente (SentManually, AppliedManually, …) é ação humana via API.

---

## 8. Digest por e-mail

`EmailDigestService` + `DigestRenderer` → HTML com as melhores oportunidades acima de um score.
`SmtpEmailSender` (config `Email:Smtp:*`). Endpoints `GET /api/digest/preview` e `POST /api/digest/send`. Worker manda diariamente (`send-daily-digest`). Flag `EnableEmailDigest`.

---

## 9. Radar de empresas (Bacen Pix Importer)

`BacenRadarService` popula o radar a partir da **lista oficial de participantes Pix do BCB** (CSV configurável em `Bacen:PixParticipantsCsvUrl`), em 2 estágios:
1. **Import** → grava em `BacenInstitution` (staging cru).
2. **Promote** → cria/atualiza `Company` só para instituições elegíveis (autorizadas, tipo IP/Banco/SCD/SCFI), com prioridade calculada e tags. **Não busca vagas** — só monta o radar.

### 9.1 Seed manual de empresas observadas (`ObservedCompaniesSeed`)

Seed **interno** (não é feature/tela/entidade nova; em `Infrastructure/Persistence`) que injeta empresas
observadas manualmente (LinkedIn etc.) no radar `Company` para o fluxo atual alcançá-las. Disparo:
`POST /api/companies/seed-observed`. Características:
- **Classificado**: financeiro (Strategic/High), consultoria/staffing (High/Medium), produto (High/Medium),
  marketplace (Medium, `noisy-source`), low/validar (`needs-validation`). Tags globais `manual-radar-seed`/`observed-linkedin`.
- **Idempotente**: dedup por nome normalizado (ignora Inc./Ltd/LTDA/S.A./Oficial/Brasil), **não duplica**,
  **não sobrescreve** WebsiteUrl/CareersUrl, só mescla tags e **eleva** prioridade (`RaisePriorityTo`).
- Marca `needs-website-discovery`/`needs-ats-detection` quando faltam; registra `ExecutionRun`.
- **Denylist**: job boards/agregadores (Indeed, Glassdoor, SimplyHired, Remotejobs, Jobbol, LinkedIn Jobs,
  Code Vagas, JobJá, Dev Life, Netvagas, Vagas PJ) e perfis pessoais **nunca** viram Company; se já existirem,
  são rebaixados via `MarkSourceOnly()`. **Sem LinkedIn**: não acessa/raspa/loga, não importa vagas.
- Cadeia recomendada depois do seed: `backfill-websites`/`{id}/discover-website` → `{id}/detect-ats`/`onboard`
  → `POST /api/jobs/discover` (o `GenericCareersCrawler` roda em qualquer empresa **com site**, achando a página de carreiras).

---

## 10. Endpoints da API (todos)

**Health:** `GET /`

**CandidateProfile** `/api/candidate-profile`: `GET /` · `GET /{id}` · `POST /` · `PUT /{id}`

**Companies** `/api/companies`: `GET /` · `GET /{id}` · `POST /` · `PUT /{id}` · `DELETE /{id}` · `POST /{id}/detect-ats` · `POST /onboard` · `POST /backfill-websites` · `POST /{id}/discover-website` · `POST /import-csv` · `POST /seed-observed` (seed manual interno, ver §9.1)

**Jobs** `/api/jobs`: `GET /` · `GET /{id}` · `POST /discover` · `POST /search` · `POST /{id}/match` · `GET /{id}/match` · `POST /validate-links` · `POST /{id}/archive`

**AI Copilot** `/api/jobs/{jobId}/ai`: `POST /analyze` · `POST /generate-outreach` · `POST /suggest-cv-tailoring` — e `POST /api/insights/career`

**Opportunities** `/api/opportunities`: `GET /` · `GET /follow-ups` · `GET /{id}` · `PUT /{id}/status` · `PUT /{id}/notes` · `PUT /{id}/follow-up`

**Recruiters** `/api/recruiters`: `GET /` · `GET /{id}` · `POST /` · `PUT /{id}` · `DELETE /{id}`

**Digest** `/api/digest`: `GET /preview` · `POST /send`

**Bacen** `/api/bacen/pix-participants`: `POST /import` · `POST /promote-to-companies` · `GET /` · `GET /{id}`

**Dashboard** (consumidos pela tela): 
- `GET /api/dashboard/summary` — cards (vagas, fortes 75+, mensagens, follow-ups) já com filtro de frescor/ativo.
- `GET /api/matches?minScore=&take=&freshDays=&maxAgeDays=&sort=&region=&contract=` — **o feed**. Último match por job; só ativos/frescos; oculta Irrelevant/HideSimilar **e** Applied/ContactedRecruiter (estes vão pro mural de aplicações). `sort=rank` usa o DiscoveryRank (§4.1); senão **score + bônus de frescor**. `region` = national/international, `contract` = clt/pj (heurística). Descarta publicadas há > `maxAgeDays` (default 120). Defaults: minScore 60, freshDays 45.
- `GET /api/applications` — **mural de aplicações**: oportunidades já marcadas como "já me cadastrei"/apliquei
  ou contatei recrutador (feedback `Applied`/`ContactedRecruiter`). Ordenado pela data da marcação. Essas vagas
  **saem do `/api/matches`** (mural principal) para o usuário ir "matando" o que já tratou.
- `DELETE /api/applications/{jobId}` — **desfazer**: remove o feedback Applied/ContactedRecruiter e a vaga volta ao mural principal.
- `GET /api/runs?take=` — feed de atividade (ExecutionRuns).
- `GET /api/messages` — rascunhos gerados.

**Feedback** `/api/feedback`: `POST /` (tipos: Relevant/Irrelevant/HideSimilar/BadCompanyDetection/Applied/ContactedRecruiter/…) · `GET /`.
Irrelevant/HideSimilar **ocultam** do mural; Applied/ContactedRecruiter **movem** para o mural de aplicações.

**Discovery (Firehose)** `/api/discovery`: `POST /campaigns` · `GET /campaigns` · `GET /campaigns/{id}` · `POST /campaigns/{id}/run` · `POST /quick-search` · `POST /aggressive-search` · `GET /raw-candidates` · `POST /raw-candidates/{id}/promote` · `POST /promote-batch` · `POST /raw-candidates/{id}/resolve-original` · `GET /metrics` · `GET /bacen-financial-sweep/preview` · `POST /bacen-financial-sweep` · `GET /provider-quality`

**Consulting Radar** `/api/consulting-radar`: `POST /discover` · `GET /candidates` · `GET /candidates/{id}` · `POST /candidates/{id}/promote-to-company` · `POST /promote-batch`

---

## 11. Worker (jobs recorrentes — Hangfire)

| Job | Cron (config) | Default | Faz |
|---|---|---|---|
| `discover-jobs` (DiscoverJobsJob) | `Jobs:DailyDiscoveryCron` | `0 8 * * *` | descoberta por empresa (ATS) |
| `continuous-discovery` (DiscoverJobsJob) | `Jobs:ContinuousDiscoveryCron` | `*/15 * * * *` | mantém o radar fresco enquanto roda |
| `search-jobs` (SearchJobsJob) | `Jobs:SearchCron` | `0 */3 * * *` | busca por palavra-chave (Gupy + Serper); espalha cota |
| `validate-links` (ValidateLinksJob) | `Jobs:ValidateLinksCron` | `30 */6 * * *` | expira links 404/410 |
| `firehose-sweep` (FirehoseSweepJob) | `Jobs:FirehoseCron` | `0 */4 * * *` | varredura Firehose (§4.1): captura RawJobCandidates |
| `promote-candidates` (PromoteCandidatesJob) | `Jobs:PromotionCron` | `30 */4 * * *` | promove RawJobCandidates → JobPosting (com triagem/dedup) |
| `bacen-financial-sweep` (BacenFinancialSweepJob) | `Jobs:BacenSweepCron` | `0 6 * * 1` | busca vagas nos bancos/fintechs do radar (condicional por flag) |
| `consulting-radar` (ConsultingRadarJob) | `Jobs:ConsultingRadarCron` | `0 7 * * 2` | descobre consultorias .NET (condicional por flag) |
| `send-daily-digest` (SendDailyDigestJob) | `Jobs:DailyDigestCron` | `0 9 * * *` | envia digest |

`SearchJobsJob` usa keywords: desenvolvedor .net, desenvolvedor backend c#, engenheiro de software .net, programador c# pleno, vaga .net remoto, desenvolvedor .net fintech, arquiteto .net, desenvolvedor c# sênior.

---

## 12. Frontend (uma tela viva)

React + Vite + TS em `frontend/`. Tela viva com **abas de destino** (não abas-vaidade). Componentes em `App.tsx`:
- **StatusRail** (esquerda) — marca, indicador "Descoberta contínua" (pulse + última atividade), **atividade do sistema** (ExecutionRuns ao vivo), card do usuário.
- **Topbar** — saudação.
- **Feed** — 4 cards de stat; **5 abas**: ✨ Pra você hoje (`action`, fit ≥75), 📋 Boas opções (`qualified`, ranqueado), 🔎 Explorar tudo (`firehose`, raw candidates), **✅ Já me cadastrei** (`applied`, mural de aplicações), 🏢 Descobrir mais (`discover`); filtros região/contrato; botão **"Procurar vagas"**; auto-refresh a cada **20s**.
- **OppCard** — logo (favicon→iniciais), título, skills, **"Por que combina"** (rationale), data, score ring, recomendação, **"Ver vaga"**, **"Gerar mensagem"** (painel inline copiável), **"Analisar com IA"**, feedback rápido (👍/👎/empresa errada/ocultar) e o botão verde **"✓ Já me cadastrei"** — que registra feedback `Applied`, **remove o card do mural** (otimista) e o joga em "Já me cadastrei".
- **ApplicationsView** (`applied`) — lista as vagas já tratadas (de `GET /api/applications`): empresa, título, rótulo da ação, quando foi marcada, score, **"Ver vaga"** e **"↩ Reabrir"** (chama `DELETE /api/applications/{jobId}` e volta pro mural).

Cliente HTTP em `api.ts` (todas as chamadas via proxy `/api`). Sem estado global além de `reload`.

---

## 13. Configuração, feature flags e segredos

**Feature flags** (`appsettings.FeatureFlags`): EnableGreenhouseProvider, EnableLeverProvider, EnableSmartRecruitersProvider, EnableAshbyProvider, EnableGupyProvider, EnableGenericCrawler, EnableGoogleWebSearch, EnableSerperWebSearch, EnableLlmAnalysis, EnableEmailDigest.

**Config relevante** (`Search`): `SerperApiKey`, `SerperMaxQueriesPerCall` (6), `SerperFreshness` (`qdr:m`), `SerperDelayMs` (1200), `ApiKey`+`SearchEngineId` (Google CSE), `DailyQueryBudget` (90).

**Segredos (`dotnet user-secrets`, nunca no git):** `Anthropic:ApiKey` (e cópia no Worker), `Search:SerperApiKey` (Api + Worker), `Email:Smtp:*`, opcional `Search:ApiKey`/`SearchEngineId`. Sem a chave correspondente, o provider fica **dormente** (degrada com elegância).

**Constantes de custo/ritmo** (em `JobDiscoveryService`): `ThinDescriptionChars=300`, `MaxEnrichmentsPerRun=8`, `LlmAutoAnalyzeGate=60`, `MaxLlmAnalysesPerRun=6`. No feed: `maxAgeDays=120`.

---

## 14. FLUXO LÓGICO (ponta a ponta)

```
┌─────────────────────────────────────────────────────────────────────────┐
│ 0. SEED / PERFIL                                                          │
│   Startup aplica migrations + seed. Existe 1 CandidateProfile ativo       │
│   (o mais recente). É contra ele que tudo é pontuado.                     │
└─────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ 1. DESCOBERTA (contínua, Worker; ou manual via API/"Buscar agora")        │
│                                                                           │
│   a) Por empresa  → IJobSourceProvider (Greenhouse/Lever/SR/Ashby/Crawler)│
│      Crawler usa Playwright se a página de carreiras for SPA.             │
│   b) Por keyword  → IJobSearchProvider (Gupy + Serper[web-aberta])        │
│                                                                           │
│   JobDiscoveryService:                                                    │
│     • dedup por (SourceProvider, ExternalId)  → atualiza, não duplica     │
│     • descrição curta? → HtmlJobContentEnricher busca texto real (cap 8)  │
│     • registra tudo em ExecutionRun (resiliente a falhas)                 │
└─────────────────────────────────────────────────────────────────────────┘
                                   │  (cada job NOVO/sem match)
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ 2. PONTUAÇÃO AUTOMÁTICA (na descoberta)                                   │
│   • JobNormalizer extrai seniority/workmode/idioma/skills/domínios        │
│   • HeuristicMatchEngine → OpportunityMatch (score + rationale seco)      │
│   • Se score ≥ 60 e orçamento LLM > 0 (cap 6/run):                        │
│        IJobUnderstandingService + ICandidateFitAnalysisService →          │
│        match LLM autoritativo (score + "por que combina" em prosa)        │
│   • Match score ≥ 70 ⇒ OpportunityPipeline cria Opportunity (Analyzed)    │
└─────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ 3. FEED (GET /api/matches) — a tela viva                                  │
│   Filtra: score ≥ minScore(60) · não Expired/Archived/TalentPool ·        │
│           descoberto há ≤ freshDays(45) · PUBLICADO há ≤ maxAgeDays(120)  │
│   Ordena: OverallScore + bônus de frescor (≤7d +15, ≤30d +10, ≤60d +5)    │
│   Mostra: último match por job · 6 por página · auto-refresh 20s          │
│   ValidateLinksJob marca 404/410 como Expired → caem do feed              │
└─────────────────────────────────────────────────────────────────────────┘
                                   │  (ação do usuário no card)
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ 4. AÇÕES HUMANAS                                                          │
│   • "Analisar com IA"  → POST /ai/analyze (rationale rico + score LLM)    │
│   • "Gerar mensagem"   → POST /ai/generate-outreach (gate score ≥ 60)     │
│        → painel inline com LinkedIn/e-mail/follow-up, copiável            │
│        → Opportunity avança até ReadyForHumanReview (teto do sistema)     │
│   • "Ver vaga"         → abre AbsoluteUrl                                  │
│   • "✓ Já me cadastrei"→ POST /api/feedback (Applied) → sai do mural e vai │
│        para "Já me cadastrei" (GET /api/applications). "↩ Reabrir" desfaz. │
│   • Pipeline/follow-up → PUT /api/opportunities/{id}/status|notes|follow-up│
│   • Digest             → GET /preview · POST /send (ou Worker diário)      │
└─────────────────────────────────────────────────────────────────────────┘
```

**Resumo de uma frase:** *o sistema descobre vagas .NET continuamente (ATS + web aberta),
enriquece e pontua cada uma (heurístico → LLM) contra o perfil, e exibe as melhores —
frescas, relevantes, com "por que combina" e mensagem pronta pra copiar — numa única tela,
sem nunca enviar nada sozinho.*

---

## 15. Como rodar / build / testar

```bash
docker compose up -d postgres                    # Postgres 18

# segredos (uma vez)
dotnet user-secrets set "Anthropic:ApiKey" "<...>" --project src/OpportunityOS.Api
dotnet user-secrets set "Search:SerperApiKey" "<...>" --project src/OpportunityOS.Api
dotnet user-secrets set "Search:SerperApiKey" "<...>" --project src/OpportunityOS.Worker
dotnet user-secrets set "Anthropic:ApiKey" "<...>" --project src/OpportunityOS.Worker

# build + testes
dotnet build OpportunityOS.slnx -c Debug
dotnet test tests/OpportunityOS.UnitTests/OpportunityOS.UnitTests.csproj   # 136 testes

# Playwright (uma vez, para crawler SPA)
pwsh src/OpportunityOS.Worker/bin/Debug/net10.0/playwright.ps1 install chromium
# (sem pwsh, use: powershell -ExecutionPolicy Bypass -File <...>/playwright.ps1 install chromium)

# rodar
$env:ASPNETCORE_URLS="http://localhost:5077"; dotnet run --project src/OpportunityOS.Api
dotnet run --project src/OpportunityOS.Worker        # jobs recorrentes
cd frontend && npm run dev                           # tela em :5173 (proxy → :5077)
```

---

## 16. Notas honestas / limitações conhecidas

- **Nome da empresa** nas vagas web-abertas vem do **host** (ex.: "Reddit", "Lever", "Br"), não da empresa real (que está no título). Melhoria pendente: extrair do conteúdo.
- **Custo LLM**: auto-análise roda na descoberta contínua do Worker (cap 6/run, gate ≥60). Consome créditos Anthropic ao longo do dia — bounded, mas existe. Ajustável (teto/gate/só-na-busca-manual).
- **Google CSE whole-web morreu** para a JSON API (jan/2026). Web-aberta hoje é **Serper.dev**. Se a chave Serper for revogada, a web-aberta pausa (resto segue).
- **Datas Gupy**: muitas vagas Gupy têm `PublishedAtUtc` de anos atrás → filtradas pelo `maxAgeDays`. Frescor real vem da web-aberta + descoberta recente.
- **Sem auth/multiusuário**: é um sistema pessoal de 1 perfil.
- **Migrations**: aplicadas no startup da API (conveniência dev).
