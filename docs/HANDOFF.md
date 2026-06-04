# Opportunity OS — Mapa completo do sistema (handoff)

> Documento de transferência para outro agente/dev. Descreve **tudo** que o sistema é,
> fornece e faz, e o **fluxo lógico** de ponta a ponta. Escrito a partir do código real
> (não da intenção). Última varredura: branch `feat/multi-profile` (fase multi-perfil).

---

## 0. Atualização — Multi-perfil de candidato (pré-auth)

O sistema deixou de ser fixo no perfil do Nícolas: agora pontua oportunidades para
**múltiplos `CandidateProfile`** (Backend .NET, Java, Frontend React, Data Engineer seedados).

- **Entidades globais** (uma vez para todos): `Company`, `JobPosting`, `RawJobCandidate`,
  `SearchCampaign`, `SearchQueryExecution`, `JobPostingSourceOccurrence`, `BacenInstitution`,
  `ConsultingCompanyCandidate`. A **descoberta continua global** — a vaga não é duplicada por perfil.
- **Entidades por perfil** (`CandidateProfileId`): `OpportunityMatch`, `Opportunity`,
  `GeneratedMessage`, `UserFeedback`/Applications, e o **digest**.
- **Resolução do perfil**: `ICurrentCandidateProfileProvider`
  (`Application/Profiles`) → `id explícito → IsDefault → mais recente`. Sem estado global de
  “ativo”; o id vai explícito por request (`?candidateProfileId=`) e o front guarda a seleção
  em `localStorage`. Substituiu os antigos `CandidateProfiles.OrderByDescending(CreatedAtUtc)`
  espalhados (AiEndpoints, JobEndpoints, EfDiscoveryStore, EfDigestStore).
- **Motor**: `HeuristicMatchEngine` (v2) + `StackTaxonomy` — TechnicalFit dirigido pela stack
  do perfil (core/secondary/excluded), pesos `Tech .50 / Cargo .15 / Sen .10 / Domínio .10 /
  Local .10 / Idioma .05` e gates. `OpportunityMatch.EngineVersion` marca a geração.
- **API**: `GET/POST /api/candidate-profiles` (+ `set-default`); `?candidateProfileId=` em
  `/api/matches`, `/api/jobs/{id}/match`, `/api/jobs/rescore` (`allProfiles=true`),
  `/api/applications`, `/api/feedback`, `/api/jobs/{id}/ai/*`, `/api/digest/*` (+ `send-all`).
- **Como criar/alternar/rodar match por perfil**: ver a seção “Multi-perfil” do `README.md`.
- **Caminho p/ auth/multi-tenancy**: a próxima fase pluga `AppUser`/Identity e resolve o perfil
  pelo usuário autenticado dentro do `ICurrentCandidateProfileProvider`; SaaS (Tenant/billing)
  vem depois. As entidades por perfil já carregam `CandidateProfileId`.

---

## 1. O que é

Sistema de **inteligência de oportunidades de emprego** que **descobre vagas globalmente** e
**pontua cada oportunidade contra um `CandidateProfile` selecionado**. O perfil principal (e
quem valida o produto) é o do Nícolas Serrano — backend **.NET/C#**, Pleno/Sênior,
fintech/pagamentos, remoto, inglês B2 — mas o motor agora suporta **múltiplos perfis técnicos**
(Java Backend, Frontend React, Data Engineer). **Fase atual: multi-perfil _pré-auth_** (sem
login, sem multiusuário real, sem tenant). Ele:

1. **Descobre** vagas continuamente e **globalmente** (ATS públicos + sites de carreira + busca web-aberta).
2. **Pontua** cada vaga **por perfil** (heurístico dirigido pelo perfil + LLM sob demanda).
3. **Mostra** numa **única tela viva** as melhores oportunidades **do perfil selecionado**, frescas e relevantes.
4. **Gera** rascunhos de mensagem/e-mail/follow-up (LLM) **por perfil** — sempre para **revisão humana**.
5. **Acompanha** num pipeline por perfil (oportunidades, follow-ups) e manda **digest por e-mail por perfil**.

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
React + Vite + TypeScript (frontend) · xUnit (149 testes).

**Portas/processos:** API em `http://localhost:5077` · Worker é processo separado ·
frontend Vite em `:5173` com **proxy `/api` → `:5077`** (não há CORS no servidor).
A API roda **migrations + seed no startup** (`SeedOnStartup=true`).

---

## 3. Modelo de domínio (entidades)

Todas com `Id` Guid e setters privados (encapsulamento). Tabelas snake_case; listas
`List<string>` viram **jsonb** via value converters.

| Entidade | Tabela | Núcleo | Métodos de domínio |
|---|---|---|---|
| `CandidateProfile` | candidate_profiles | FullName, **DisplayName**, **IsDefault**, Headline, Summary, Location, Seniority, PreferredLanguage, **CoreSkills**, **SecondarySkills**, **ExcludedStacks**, Domains, **PreferredRoles**, **PreferredContractTypes**, **PreferredLocations**, **PreferredWorkModes**, **MinimumScoreToShow**, Experiences (jsonb) | `Update(...)`, `SetDefault(bool)` — perfis **selecionáveis**; `IsDefault` é a âncora de fallback |
| `Company` | companies | Name, WebsiteUrl, CareersUrl, LinkedInUrl, Industry, Country, Priority, Source, Tags | `Update`, `MarkScanned`, `SetCareersUrl`, `SetWebsiteUrl`, `AddTag`, `RaisePriorityTo` (sobe sem rebaixar), `MarkSourceOnly` (rebaixa denylist a Low + source-only/noisy-source/do-not-promote) |
| `JobPosting` | job_postings | CompanyId, ExternalId, **SourceProvider**, Title, Location, WorkMode, Seniority, Language, AbsoluteUrl, DescriptionText/Html, ExtractedSkills/Domains, **Status**, PublishedAtUtc, CreatedAtUtc, UpdatedAtUtc | `RefreshFromSource`, `ApplyNormalization`, `MarkAnalyzed`, `Archive`, `MarkExpired` |
| `OpportunityMatch` | opportunity_matches | JobPostingId, **CandidateProfileId**, OverallScore + 5 subscores, Recommendation, Strengths/Risks/MissingRequirements, **Rationale**, **EngineVersion** | **imutável** e **específico por perfil** — a mesma vaga tem um match por `CandidateProfile`; `EngineVersion` marca a geração (`heuristic-v2`/`llm-fit-v1`) |
| `Opportunity` | opportunities | JobPostingId, **CandidateProfileId**, RecruiterLeadId?, **Status**, NextFollowUpAtUtc, Notes | `AdvanceTo`, `SetStatusManually`, `SetNotes`, `SetFollowUp`, `LinkRecruiter`, `Create(job, profile)` — **única por (JobPostingId + CandidateProfileId)**; a mesma vaga vira oportunidades distintas para perfis distintos |
| `GeneratedMessage` | generated_messages | JobPostingId, OpportunityMatchId, **CandidateProfileId**, LinkedInMessage, CoverLetter, EmailSubject/Body, CvTailoringNotes, FollowUpMessage, HumanReviewNotes, Status, PromptVersion, ModelName | `SetStatus` — **específica do perfil** (a mesma vaga gera rascunhos diferentes por perfil) |
| `ExecutionRun` | execution_runs | RunType, Status, Started/Finished, ItemsProcessed/Succeeded/Failed, ErrorMessage | `RecordSuccess`, `RecordFailure`, `Complete`, `Fail`, `Start` |
| `PromptExecutionLog` | prompt_execution_logs | Service, PromptVersion, ModelName, Success, UsedFallback, RawResponse, JobPostingId? | auditoria de cada chamada LLM |
| `RecruiterLead` | recruiter_leads | CompanyId, FullName, Source, … | CRUD |
| `BacenInstitution` | bacen_institutions | Name, Ispb, Cnpj, InstitutionType, AuthorizedByBacen, Pix/Spi fields, Tags | `UpdateFrom` (staging do radar Bacen) |
| `RawJobCandidate` | raw_job_candidates | Title, Snippet, DiscoveredUrl, SourceProvider/Name, **SourceType**, RealCompanyName, OriginalJobUrl, Location/WorkMode/Language, **Status** (RawJobCandidateStatus), SourceConfidenceScore, PreliminaryFitScore, **NormalizedFingerprint**, RequiresManualValidation, **VerificationStatus**, SearchCampaignId, Query, PromotedJobPostingId | `Classify`, `Enrich`, `SetVerification`, `MarkDuplicate`, `PromoteTo`, `Reject` — **núcleo do Firehose** (§4.1) |
| `SearchCampaign` | search_campaigns | Name, Description, **Status**/**Priority**, BaseKeywords, DailyQueryBudget, LastRunAtUtc | conjuntos de busca salvos (rodáveis) |
| `SearchQueryExecution` | search_query_executions | SearchCampaignId, Query, **Status**, ResultsCount, StartedAt | auditoria de cada query do Firehose |
| `JobPostingSourceOccurrence` | job_posting_source_occurrences | JobPostingId, SourceProvider/Name, Url, SeenAtUtc | cada vez que a MESMA vaga aparece em outra fonte (dedup por fingerprint) |
| `UserFeedback` | user_feedbacks | **Type** (UserFeedbackType), JobPostingId?, RawJobCandidateId?, **CandidateProfileId?**, Reason | feedback do usuário (relevante/ocultar/aplicada/…) **por perfil** — feedback de um perfil **não** afeta outro; alimenta DiscoveryRank e o mural de **Applications** (derivadas de feedback Applied/ContactedRecruiter **por perfil**) |
| `ConsultingCompanyCandidate` | consulting_company_candidates | Name, WebsiteUrl, Country, Source, Signals, **ConsultingConfidenceScore**, **Status** | candidatas do radar de consultorias (antes de virar `Company`) |

`JobPosting` tem propriedades calculadas (não-mapeadas): **`EffectiveDateUtc` = PublishedAtUtc ?? SourceUpdatedAtUtc ?? CreatedAtUtc** (data real de publicação → última atualização da fonte → só por último a descoberta), **`HasSourceDate`** (true quando a data veio da FONTE, não da descoberta — a UI mostra *"publicada há X"* vs *"encontrada há X"*) e **`IsTalentPool`**. Além do núcleo, carrega **qualidade de fonte**: `SourceType`, `SourceName`, `SourceConfidenceScore`, `RequiresManualValidation`, `RealCompanyName`, `OriginalJobUrl`, `NormalizedFingerprint` (`SetSourceQuality`/`RefreshFromSource`).

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
- **SerperWebJobSearchProvider** — **busca web-aberta real** via **Serper.dev** (`google.serper.dev/search`, índice Google). Deriva empresa do host, filtra agregadores, frescor via `tbs=qdr:m`. **Captura a data real** do resultado (`date`: absoluta ou "N days ago"/"há N dias") → `PublishedAtUtc`. Respeita rate-limit do tier grátis. **Requer `Search:SerperApiKey`** (senão dormente).
- **GoogleWebJobSearchProvider** — busca CSE Google. **Dormente/descontinuado para web-aberta**: a JSON API do Google não serve mais engines whole-web (403 desde jan/2026). Só funciona com `cx` escopado a sites. Mantido no código atrás de `Search:ApiKey`+`Search:SearchEngineId`.

### Provedores de apoio à descoberta
- **`IAtsDetector` (AtsDetector)** — reconhece o ATS de uma empresa pela URL/HTML: Greenhouse, Lever, Gupy, Workday, Ashby, SmartRecruiters, Workable, Recruitee, Teamtailor, Breezy, inhire, Abler, Solides, Pandapé, Kenoby, Quickin, JobConvo, Taqe, 99jobs, Recrutei, GeekHunter, Coodesh, Programathor.
- **`IAtsBoardFinder`** — acha o board de vagas de uma empresa. `GoogleAtsBoardFinder` (se CSE configurado) → fallback `HeuristicAtsBoardFinder`.
- **Descoberta de careers/ATS em lote** — `POST /api/companies/detect-ats-bulk?limit=N`: rasteja cada empresa com **site mas sem careers/ATS**, salva o **board** quando acha um ATS, senão a **página de carreiras** (pro crawler genérico minerar). Foi o que tirou o radar de 5 → ~116 empresas mineráveis. O `detect-ats` por-empresa também passou a **salvar a página de carreiras** (antes descartava quando não achava ATS).
- **Fontes adicionais reconhecidas (SourceClassifier + filtros `site:`):** Recrutei (corrigido de agregador → **ATS**), Infojobs, INTERA, Michael Page, Vagas.com (promovido a job board). Indeed segue como agregador de propósito.
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
  *modelo negativo* de TODO feedback `Irrelevant`/`HideSimilar` — **empresa** + **palavras do motivo**
  (NÃO aprende skills, pra não envenenar ".NET" quando você recusa uma vaga .NET por outro motivo).
  No `/api/matches` aplica **penalidade** e **oculta** quando forte (mesma empresa ≥3× ou penalidade alta).
  Deriva também **preferências estruturadas**: `IntlDislikes` (motivo "Internacional"/"Exterior") → com ≥3
  esconde **vagas internacionais** por padrão (localização não é palavra do anúncio, então isso captura seu
  sinal #1); `OnsiteDislikes` (Presencial/Híbrido). Acento-insensível. + feedback "encerrada/movida" **expira** a vaga.
- **`OpportunityHeuristics` (compartilhado mural + digest)** — `IsInternational`, `CompanyFromTitle`,
  `BestCompany` (empresa real → do título "at/na/em X" → board oficial não-host → senão "Empresa a confirmar"),
  `DedupKey` (colapsa a MESMA vaga vista em vários agregadores). Usado pelo `/api/matches` E pelo e-mail diário,
  pra os dois mostrarem a mesma qualidade.

---

## 5. Match engine (relevância)

### `HeuristicMatchEngine` v2 (sem LLM, transparente, **dirigido pelo perfil**)
`OverallScore = Técnico*0.50 + Cargo*0.15 + Senioridade*0.10 + Domínio*0.10 + Localização*0.10 + Idioma*0.05`

> v2 (multi-perfil): o TechnicalFit é calculado contra a **stack do perfil** via
> `StackTaxonomy` (`Matching/StackTaxonomy.cs`), não mais com viés fixo em .NET. Cada match
> grava `EngineVersion` (`heuristic-v2` / `llm-fit-v1`). A v1 (.NET-fixa, pesos
> `0.45/0.20/0.15/0.10/0.10`) foi substituída.

- **Técnico**: famílias do perfil (core/secondary/excluded) vs famílias da vaga. Família **core**
  presente na vaga = base alta (72 + bônus por skills do perfil citadas); **secondary** = média
  (~48–68); stack concorrente/desconhecida = baixa; **excluded** = teto 25.
- **Cargo**: overlap do título com `PreferredRoles`; título citando a stack core do perfil reforça (≥75).
- **Senioridade**: alvo do perfil (`Seniority`) → compatível 90, ±1 nível 68, distante 35–50; não informado 65.
- **Domínio**: overlap com `profile.Domains` (bônus, nunca gate; sem sinal = 50).
- **Localização**: `PreferredWorkModes` do perfil; Remote 88–95, Hybrid 62–80, Onsite 35–80.
- **Idioma**: `PreferredLanguage`/pt-BR/en = 90; outro = 45.
- **Recomendação**: ≥90 Strategic · ≥75 Prioritize · ≥60 Apply · ≥40 SaveForLater · senão Ignore.
- Rationale heurístico é seco: `"Score 88/100 — Técnico 100, Cargo 85, Senioridade 90, …"`.
- **GATES:** `Técnico<35 → ≤45`; cargo gestão/negócio sem sinal dev → `≤40`; vaga fora da stack
  **core** do perfil → `≤70` (nunca topo).
  (Os gates impedem que domínio/senioridade/localização/idioma levem ao topo uma vaga fora da
  stack do perfil — ex.: "Director, Collections" numa fintech, ou um cargo Java para um perfil .NET.)
- **Re-pontuar o acervo:** `POST /api/jobs/rescore` reavalia com o engine atual (gate retroativo). Por
  padrão só o perfil resolvido/default; `?allProfiles=true` reavalia **todos os perfis** (gera um match por
  (vaga, perfil)); aceita `?take=` e `?engineVersion=`. Como `OpportunityMatch` é imutável, grava um match
  novo só quando o score muda — e o `/api/matches` sempre pega o **último por (vaga, perfil)**.

`JobNormalizer` (+ `KnownTerms`) deriva Seniority, WorkMode, Language, Skills, Domains do título/descrição.

---

## 6. Camada de IA (AI Copilot)

`ILlmProvider` selecionado em runtime (`Llm:Provider` = auto|anthropic|openai|fake):
- **AnthropicLlmProvider** (claude-sonnet-4-6, default se há `Anthropic:ApiKey`)
- **OpenAiLlmProvider** (gpt-4.1-mini)
- **FakeLlmProvider** (determinístico, JSON válido — usado se sem chave ou `EnableLlmAnalysis=false`)

Cinco serviços (cada um com prompt versionado e auditado em `PromptExecutionLog`):
1. **IJobUnderstandingService** (`job-analysis-v1`) — interpreta a vaga → skills, domínios, senioridade, work mode, idioma, responsabilidades, riscos, resumo.
2. **ICandidateFitAnalysisService** (`fit-score-v1`) — compara **o perfil selecionado** × vaga → `OpportunityMatch` (com `CandidateProfileId` + `EngineVersion=llm-fit-v1`) com score + rationale rico. *Aderência técnica é o que domina, **relativa à stack do perfil** (core/secondary/excluded): para o perfil Nícolas, .NET/C# é match forte; para o perfil Java, Java/Spring; para React, React/TS; etc. Domínio (ex.: pagamentos) é bônus, não gate.*
3. **IOutreachDraftService** (`outreach-v1`) — rascunhos: mensagem LinkedIn/direta, cover letter, assunto+corpo de e-mail, follow-up, observações de revisão humana.
4. **ICvTailoringSuggestionService** (`cv-tailoring-v1`) — sugestões de ajuste de CV (advisory).
5. **ICareerInsightService** (`career-insight-v1`) — padrões entre várias vagas analisadas.

Gate: **outreach exige score ≥60** (`MinScoreForOutreach`).

---

## 7. Pipeline de oportunidades

`OpportunityPipeline` (**por perfil** — `FindByJobAsync(jobId, candidateProfileId)`):
- **Auto-cria** `Opportunity` quando um match tem score **≥70** (`AutoCreateThreshold`), status inicial `Analyzed`, **para o `CandidateProfileId` do match**. A mesma vaga vira oportunidades distintas para perfis distintos (única por `JobPostingId + CandidateProfileId`).
- Ao gerar mensagem, avança a oportunidade **daquele perfil** — mas o **sistema só pode chegar até `ReadyForHumanReview`**. Daí pra frente (SentManually, AppliedManually, …) é ação humana via API.

---

## 8. Digest por e-mail

`EmailDigestService` + `DigestRenderer` → HTML com as **3 melhores** oportunidades "pra aplicar hoje"
(não um dump). **O digest é POR PERFIL:** `IDigestStore.GetDigestItemsAsync(candidateProfileId, minScore)`
pega o **último match por (vaga, perfil) ANTES do gate de score** (filtra `OpportunityMatch`,
`UserFeedback` ocultas/aplicadas e `GeneratedMessage` **deste perfil**), aplica as **mesmas regras do
mural** via `OpportunityHeuristics` (esconde internacional quando o perfil tem `IntlDislikes≥3`, empresa
real via `BestCompany`, **dedup**), ordena por score e pega o **top-3**. A greeting usa o `FullName`
**daquele** perfil. `SmtpEmailSender` (config `Email:Smtp:*`; `From` vazio → usa a conta autenticada).
- **Endpoints:** `GET /api/digest/preview?candidateProfileId=` · `POST /api/digest/send?candidateProfileId=`
  (um perfil; default se omitido) · `POST /api/digest/send-all` (itera **todos** os perfis, cada um com seu
  `MinimumScoreToShow`).
- **Worker** (`send-daily-digest`, diário às 9h) chama `SendAllAsync` → **um digest por perfil**. Flag `EnableEmailDigest`.

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

**CandidateProfile** (multi-perfil):
- `/api/candidate-profiles` (plural): `GET /` (lista) · `GET /{id}` · `POST /` · `PUT /{id}` · `POST /{id}/set-default` (move a âncora padrão).
- `/api/candidate-profile` (singular, back-compat): `GET /` retorna o perfil **default/atual** · `GET /{id}` · `POST /` · `PUT /{id}`.
- O `candidateProfileId` é aceito em: `/api/matches`, `/api/jobs/{id}/match`, `/api/jobs/rescore` (+`allProfiles`), `/api/applications` (+`DELETE`), `/api/feedback`, `/api/jobs/{jobId}/ai/{analyze,generate-outreach,suggest-cv-tailoring}`, `/api/digest/{preview,send}`. Ausente ⇒ perfil default.

**Companies** `/api/companies`: `GET /` · `GET /{id}` · `POST /` · `PUT /{id}` · `DELETE /{id}` · `POST /{id}/detect-ats` · `POST /detect-ats-bulk?limit=N` (careers/ATS em lote, §4) · `POST /onboard` · `POST /backfill-websites` · `POST /{id}/discover-website` · `POST /import-csv` · `POST /seed-observed` (seed manual interno, ver §9.1)

**Jobs** `/api/jobs`: `GET /` · `GET /{id}` · `POST /discover` · `POST /search` · `POST /rescore?candidateProfileId=&allProfiles=&take=&minCreatedAtUtc=&engineVersion=&onlyWithoutCurrentEngineVersion=` (re-pontua **por perfil**; default = só 1 perfil, §5) · `POST /{id}/match?candidateProfileId=` · `GET /{id}/match?candidateProfileId=` · `POST /validate-links` · `POST /{id}/archive`

**AI Copilot** `/api/jobs/{jobId}/ai` (todos aceitam `?candidateProfileId=`): `POST /analyze` · `POST /generate-outreach` · `POST /suggest-cv-tailoring` — e `POST /api/insights/career`

**Opportunities** `/api/opportunities`: `GET /?candidateProfileId=` (filtra por perfil; omitido = todos) · `GET /follow-ups` · `GET /{id}` · `PUT /{id}/status` · `PUT /{id}/notes` · `PUT /{id}/follow-up`

**Recruiters** `/api/recruiters`: `GET /` · `GET /{id}` · `POST /` · `PUT /{id}` · `DELETE /{id}`

**Digest** `/api/digest`: `GET /preview?candidateProfileId=` · `POST /send?candidateProfileId=` · `POST /send-all` (um digest por perfil — o que o job diário faz)

**Debug (dev)** `/api/debug`: `GET /job/{jobId}/profile-scores` — score/recomendação/`engineVersion` da MESMA vaga para **cada** perfil (validação de que o motor raciocina diferente por perfil).

**Bacen** `/api/bacen/pix-participants`: `POST /import` · `POST /promote-to-companies` · `GET /` · `GET /{id}`

**Dashboard** (consumidos pela tela): 
- `GET /api/dashboard/summary` — cards (vagas, fortes 75+, mensagens, follow-ups) já com filtro de frescor/ativo.
- `GET /api/matches?candidateProfileId=&minScore=&take=&freshDays=&maxAgeDays=&sort=&region=&contract=` — **o feed, POR PERFIL**. Resolve o perfil (id explícito → default) e **pega o ÚLTIMO match por (JobPostingId + CandidateProfileId) ANTES do gate de score** — nunca o último match global por job (senão o feed de um perfil seria contaminado pelo score de outro, ou por um match velho inflado). Só ativos/frescos; oculta Irrelevant/HideSimilar **e** Applied/ContactedRecruiter **deste perfil**; esconde internacional se este perfil recusa (§4.1); **dedup**; nome via `BestCompany`. `region`=national/international, `contract`=clt/pj/both/unknown. Descarta publicadas há > `maxAgeDays` (120). `take` clamp 600. Defaults: minScore 60, freshDays 45.
- `GET /api/applications?candidateProfileId=` — **mural de aplicações DO PERFIL**: vagas marcadas como "já me cadastrei"/apliquei ou contatei recrutador (feedback `Applied`/`ContactedRecruiter` **deste perfil**). Saem do `/api/matches` daquele perfil. O score exibido é o **deste perfil**.
- `DELETE /api/applications/{jobId}?candidateProfileId=` — **desfazer** (só naquele perfil): remove o feedback Applied/ContactedRecruiter e a vaga volta ao mural principal do perfil.
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
| `send-daily-digest` (SendDailyDigestJob) | `Jobs:DailyDigestCron` | `0 9 * * *` | `SendAllAsync` → **um digest por perfil** |

`SearchJobsJob` usa keywords: desenvolvedor .net, desenvolvedor backend c#, engenheiro de software .net, programador c# pleno, vaga .net remoto, desenvolvedor .net fintech, arquiteto .net, desenvolvedor c# sênior.

---

## 12. Frontend (uma tela viva)

React + Vite + TS em `frontend/`. **Estética glassmorphism premium**: fundo abstrato pastel (recriado em CSS no `body` — gradientes difusos + dots discretos; trocável por `/assets/opportunity-bg.png`), sidebar/cards/painéis em vidro (`backdrop-filter: blur`). Componentes em `App.tsx`:
- **Sidebar** — marca, **3 itens** (Oportunidades, Empresas, Aplicações) + grupo discreto **"Sistema"** (Descobertas, Relatórios, **Perfis**); card **"Radar ativo"**; e o **seletor de perfil** (card do usuário): dropdown "Perfil ativo" com os perfis (default marcado ★) + "Gerenciar perfis ›".
- **Seletor de perfil (multi-perfil)** — a seleção fica em **`localStorage`** (`oos.selectedProfileId`); **não** há estado global de "ativo" no servidor. Trocar o perfil recarrega **matches, applications, summary e digest preview** (cada chamada manda `?candidateProfileId=`) e atualiza a **copy da tela** ("…ao perfil Java Backend"). **Não há login nem `UserId` ainda.** A tela **Perfis** permite criar/editar perfis e mover o `IsDefault`.
- **OpportunitiesScreen** — header pessoal + "Atualizado há X"; **um feed único e direto** (sem abas) "Oportunidades pra você", ordenado por **aderência**; busca + **Filtros** (Onde: Todas/Brasil/Exterior · Contrato: **Todos/CLT/PJ/Ambos/Não informado** bucket exato · Ordenar: Aderência/Recentes) + **✨ Buscar agora** (painel de progresso amigável → resultado real). **Fit-to-viewport**: mostra só os cards que cabem na tela + paginação fixa (sem scroll); auto-refresh 25s.
- **OppCard** — empresa + **fonte** (Site oficial / Encontrada na web / Fonte menos confiável) + **data honesta** ("publicada há X" se da fonte, *"encontrada há X"* itálico se só descoberta) + título limpo (`cleanTitle`) + subtítulo + ≤5 tags · resumo curto · **score + rótulo humano** (Abrir primeiro ≥90 / Vale olhar ≥80 / Boa opção ≥75) · ações **Ver vaga · Rascunho · Feito** + **X** (remover, abre painel "por que não serve" que ensina o sistema) + menu "…" (ocultar/irrelevante/empresa errada/detalhes).
- **Empresas** (busca + "Atualizar busca"), **Aplicações** (mural do que já tratou, "↩ Reabrir"), **Descobertas**, **Relatórios** (métricas vivem aqui, fora do mural principal).

Cliente HTTP em `api.ts` (proxy `/api`). Sem estado global além de `reload`.

---

## 13. Configuração, feature flags e segredos

**Feature flags** (`appsettings.FeatureFlags`): EnableGreenhouseProvider, EnableLeverProvider, EnableSmartRecruitersProvider, EnableAshbyProvider, EnableGupyProvider, EnableGenericCrawler, EnableGoogleWebSearch, EnableSerperWebSearch, EnableLlmAnalysis, EnableEmailDigest.

**Config relevante** (`Search`): `SerperApiKey`, `SerperMaxQueriesPerCall` (6), `SerperFreshness` (`qdr:m`), `SerperDelayMs` (1200), `ApiKey`+`SearchEngineId` (Google CSE), `DailyQueryBudget` (90).

**Segredos (`dotnet user-secrets`, nunca no git):** `Anthropic:ApiKey` (e cópia no Worker), `Search:SerperApiKey` (Api + Worker), `Email:Smtp:*` (Gmail: Host smtp.gmail.com, Port 587, Username = e-mail, Password = **app password**; `Email:To` = destinatário; `Email:From` vazio → usa o Username), opcional `Search:ApiKey`/`SearchEngineId`. Sem a chave, o provider fica **dormente**.

**Trava de RAM do Docker (Windows/WSL2):** a VM do WSL2 (onde o Docker roda) balloona a RAM se não limitada. `C:\Users\<você>\.wslconfig` → `[wsl2] memory=6GB / processors=4 / swap=2GB` + `[experimental] autoMemoryReclaim=gradual`. Aplicar com `wsl --shutdown` + reabrir o Docker. O Postgres tem `mem_limit: 512m` no `docker-compose.yml` (defesa extra).

**Constantes de custo/ritmo** (em `JobDiscoveryService`): `ThinDescriptionChars=300`, `MaxEnrichmentsPerRun=8`, `LlmAutoAnalyzeGate=60`, `MaxLlmAnalysesPerRun=6`. No feed: `maxAgeDays=120`.

---

## 14. FLUXO LÓGICO (ponta a ponta)

```
┌─────────────────────────────────────────────────────────────────────────┐
│ 0. SEED / PERFIS                                                          │
│   Startup aplica migrations + seed. Existem MÚLTIPLOS CandidateProfiles.  │
│   Cada request resolve o perfil por candidateProfileId; se ausente, usa   │
│   o IsDefault; se não houver, o mais recente (compat). Sem "ativo" global.│
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
│ 2. PONTUAÇÃO AUTOMÁTICA (na descoberta — contra o perfil DEFAULT)         │
│   • JobNormalizer extrai seniority/workmode/idioma/skills/domínios        │
│   • HeuristicMatchEngine v2 → OpportunityMatch p/ o perfil default        │
│   • Se score ≥ 60 e orçamento LLM > 0 (cap 6/run):                        │
│        IJobUnderstandingService + ICandidateFitAnalysisService →          │
│        match LLM autoritativo (score + "por que combina" em prosa)        │
│   • Match score ≥ 70 ⇒ OpportunityPipeline cria Opportunity (Analyzed)    │
│   • Outros perfis recebem matches via POST /api/jobs/rescore (por perfil  │
│     ou allProfiles=true) — gera um match por (vaga, perfil).              │
└─────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ 3. FEED (GET /api/matches?candidateProfileId=) — a tela viva, POR PERFIL  │
│   ÚLTIMO match por (JobPostingId + CandidateProfileId) → gate (≥60) ·      │
│   não Exp/Arq/TalentP. · fresco · gate técnico · esconde intl/declinadas   │
│   (feedback DESTE perfil) · dedup. Empresa via BestCompany. Ordena por fit.│
│   UI: feed único, fit-to-viewport (sem scroll) · auto-refresh 25s         │
│   ValidateLinksJob + feedback "encerrada" marcam Expired → caem do feed   │
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

**Resumo de uma frase:** *o sistema descobre vagas **globalmente** (ATS + web aberta),
enriquece e pontua cada uma (heurístico v2 → LLM) **contra o `CandidateProfile` selecionado**,
e exibe as melhores **daquele perfil** — frescas, relevantes, com "por que combina" e mensagem
pronta pra copiar — numa única tela, sem nunca enviar nada sozinho.*

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
dotnet test tests/OpportunityOS.UnitTests/OpportunityOS.UnitTests.csproj   # 149 testes

# Playwright (uma vez, para crawler SPA)
pwsh src/OpportunityOS.Worker/bin/Debug/net10.0/playwright.ps1 install chromium
# (sem pwsh, use: powershell -ExecutionPolicy Bypass -File <...>/playwright.ps1 install chromium)

# rodar tudo de uma vez (Windows): mata API/Worker antigos, builda, sobe Postgres+API+Worker+Front
.\dev-up.cmd

# ou manual:
$env:ASPNETCORE_URLS="http://localhost:5077"; dotnet run --project src/OpportunityOS.Api
dotnet run --project src/OpportunityOS.Worker        # jobs recorrentes (descoberta contínua + digest 9h)
cd frontend && npm run dev                           # tela em :5173 (proxy → :5077)
```

---

## 16. Notas honestas / limitações conhecidas

- **Nome da empresa**: `BestCompany` mostra a real (nome resolvido → do título "at/na/em X" → board oficial não-host); quando não dá pra confiar, mostra **"Empresa a confirmar"** em vez de mentir. ~46% das vagas web-abertas caem em "a confirmar" (agregadores não expõem a empresa) — melhoria futura: ler `hiringOrganization` (JSON-LD) da página.
- **Custo LLM**: auto-análise na descoberta contínua (cap 6/run, gate ≥60). Bounded, mas consome Anthropic.
- **Google CSE whole-web morreu** (jan/2026); web-aberta hoje é **Serper.dev**.
- **Datas**: `EffectiveDateUtc` usa publicação/atualização real quando existe; ~as do CareersCrawler (1027) ainda **sem data** (faltou ler `datePosted` do JSON-LD) → mostradas como *"encontrada há X"*.
- **Multi-perfil _pré-auth_**: o sistema suporta múltiplos `CandidateProfile`, mas o perfil é
  escolhido por `candidateProfileId` + `localStorage` — **ainda não há isolamento por usuário,
  login nem Tenant**. Multi-profile **prepara**, mas **não substitui**, auth/multi-tenancy: a
  próxima fase deve plugar `AppUser`/Identity no `ICurrentCandidateProfileProvider`. A descoberta
  na inicialização auto-pontua só o **perfil default**; os demais dependem de `rescore`.
- **`/api/matches` precisa da otimização `LatestOpportunityMatch`** (ver §17, débito #1) — com
  multi-perfil o volume de matches cresce por `vagas × perfis × versões`.

## 17. Débito técnico conhecido (priorizado)

- 🔴 **#1 (PRIORIDADE TÉCNICA) — projeção `LatestOpportunityMatch`.** Hoje `/api/matches` e o
  digest carregam **todos** os matches e fazem o "latest-per-(job,perfil)" **em memória**. Com
  multi-perfil isso cresce por `nº vagas × nº perfis × nº versões de score` e degrada rápido.
  **Recomendado:** uma tabela/projeção `LatestOpportunityMatch` { `JobPostingId`,
  `CandidateProfileId`, `OpportunityMatchId`, `OverallScore`, `Recommendation`, `EngineVersion`,
  `CreatedAtUtc`, `UpdatedAtUtc` } com **única por (JobPostingId + CandidateProfileId)**; o
  `rescore`/auto-score faz **upsert** ao criar um match novo; `/api/matches` e o digest consultam
  **só a projeção** (filtrável por perfil, paginável no servidor). Deixa o feed estável e previsível.
- 🟠 **#2 (PRIORIDADE DE PRODUTO) — tracking de RESULTADO** (`ApplicationOutcome` / `ApplicationEvent`
  por perfil): `Viewed · DraftCopied · Applied · MessageSent · Replied · InterviewScheduled ·
  InterviewPassed · OfferReceived · Rejected · Archived`. Hoje o sistema só aprende dos **descartes**;
  precisa aprender também do que **gera resposta/entrevista/proposta** — o maior salto de assertividade.
- 🟠 **`detect-ats-bulk` não converge**: re-rastreia as ~229 empresas que falham toda rodada (falta tag `no-careers-found`).
- 🟡 **Resiliência do startup**: a API lança exceção e morre se o Postgres estiver fora (sem retry/espera) — fonte de dor operacional.
- 🟡 **Endpoints sem teste**: só serviços puros (match engine, FeedbackLearning, classifier) têm teste; a lógica do `/api/matches` (filtros, intl, contrato, dedup, datas) é integração não-coberta.
- 🟡 **CareersCrawler sem data** (ver acima) e **código morto** no front (`actionToday`/`allOpportunities` não usados, CSS `.seg`/`.tabsrow` órfãos).
- ✅ Já pagos nesta rodada: gate técnico, dedup, BestCompany, bug do *latest-match-before-filter* (matches **e** summary), datas honestas, contrato preciso, e-mail top-3 alinhado, trava de RAM do WSL/Docker.
