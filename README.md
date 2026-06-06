# Opportunity OS

Sistema de inteligência de oportunidades profissionais que **descobre vagas globalmente** e
**pontua cada oportunidade contra um `CandidateProfile` selecionado**. O perfil principal
continua sendo o do Nícolas (backend **.NET/C#**), mas o motor agora suporta **múltiplos
perfis técnicos** (Java Backend, Frontend React, Data Engineer). Mapeia empresas-alvo,
mede aderência **por perfil**, gera abordagens revisáveis e envia um digest por e-mail para
**revisão humana**.

> **Fase atual: multi-perfil _pré-auth_.** Ainda **não** há login, multiusuário real nem
> tenant — o perfil é escolhido por `candidateProfileId` (lembrado em `localStorage`). É uma
> preparação técnica para validar se o motor generaliza antes de auth/multi-tenancy.

> **Não** automatiza LinkedIn, **não** faz scraping logado, **não** aplica para vagas e
> **não** envia mensagens sem revisão humana. É um copiloto de carreira, não um robô de spam.

Este repositório está sendo construído por fases (Spec-Driven Development). **Esta entrega
cobre as Fases 1 (Core funcional), 2 (Automação diária / descoberta), 3 (AI Copilot Layer)
, 4 (CRM de oportunidades) e 5 (Email Digest). O roadmap planejado está completo.**

---

## Stack

- .NET 10 LTS · ASP.NET Core Minimal API
- Entity Framework Core 10 + PostgreSQL 18 (campos de listas como `jsonb`)
- Docker Compose para o banco
- xUnit (testes unitários e de integração)

## Arquitetura (Clean Architecture simplificada)

```
src/
  OpportunityOS.Domain          # Entidades, enums, regras de negócio (sem dependências)
  OpportunityOS.Contracts       # DTOs de request/response da API
  OpportunityOS.Application      # Casos de uso: Match Engine, normalização, descoberta, interfaces
  OpportunityOS.Infrastructure   # EF Core, DbContext, migrations, DI, ATS providers
  OpportunityOS.Api              # Minimal API (endpoints), seed, OpenAPI
  OpportunityOS.Worker           # Host Hangfire: jobs recorrentes (descoberta diária)
tests/
  OpportunityOS.UnitTests        # Match Engine + Normalizer
  OpportunityOS.IntegrationTests # API + PostgreSQL real (auto-skip se não houver DB)
```

Direção das dependências: `Api → Infrastructure → Application → Domain`.
`Contracts` é compartilhado e depende apenas de `Domain`.

## Pré-requisitos

- .NET SDK 10
- Docker + Docker Compose

## Subir tudo com um comando

Na raiz do repo (Windows), sobe Postgres + API + Worker + Frontend em janelas separadas:

```cmd
dev-up
```
(ou `powershell -ExecutionPolicy Bypass -File dev-up.ps1`). Depois abra **http://localhost:5173**.
Para derrubar: `dev-down`.

> Use este caminho (`dotnet run`) para ter Claude/Google/e-mail reais — as chaves ficam no
> User Secrets, que o container Docker não enxerga. As etapas manuais abaixo são equivalentes.

## Como rodar localmente

1. **Suba o PostgreSQL:**

   ```bash
   docker compose up -d postgres
   ```

2. **Rode a API** (aplica migrations e faz seed automaticamente no startup):

   ```bash
   dotnet run --project src/OpportunityOS.Api
   ```

   A porta de desenvolvimento vem de `src/OpportunityOS.Api/Properties/launchSettings.json`.
   O documento OpenAPI fica em `/openapi/v1.json`.

3. **(Opcional) Rode o Worker** (Hangfire — agenda a descoberta diária):

   ```bash
   dotnet run --project src/OpportunityOS.Worker
   ```

   No primeiro start ele instala o schema do Hangfire no PostgreSQL e registra o job
   recorrente `discover-jobs` (cron `Jobs:DailyDiscoveryCron`, padrão `0 8 * * *`).

4. **(Opcional) Suba tudo via Docker Compose** (API + Worker + banco):

   ```bash
   docker compose up --build
   ```

   A API fica em `http://localhost:5000`.

### Seed inicial

No primeiro start (banco vazio) o sistema cria:

- O **perfil do candidato** (Nícolas Serrano, backend .NET / pagamentos).
- Duas empresas e duas vagas de exemplo (uma fintech .NET/payments e uma frontend React)
  para testar o Match Engine imediatamente.

Desabilite com `"SeedOnStartup": false` em `appsettings.json` ou via env var
`SeedOnStartup=false`.

## Auth Workspace MVP (login + isolamento por usuário)

O Opportunity OS agora é um **produto logado**. Cada usuário tem um **Workspace** privado que
agrupa seus dados pessoais (perfis, feedback, rascunhos, aplicações). Dados globais (empresas,
vagas, descoberta) continuam compartilhados.

**Como funciona**
- Autenticação: **ASP.NET Core Identity + cookie** (`oos.auth`, HttpOnly; `Secure` só em produção).
- Ao registrar, cria-se o `AppUser` **e** seu `Workspace` (1 por usuário no MVP).
- Cada `CandidateProfile` pertence a um `Workspace` (`candidate_profiles.workspace_id`).
- **Regra de isolamento** (`§19`): toda resolução de perfil passa por
  `ICurrentCandidateProfileProvider`, agora *workspace-scoped*. Um `candidateProfileId` de outro
  workspace resulta em **403** (`ForbiddenProfileAccessException`), nunca em vazamento de dados.

**Proteção de endpoints**
- *Pessoais* (`RequireAuthorization`): `candidate-profile(s)`, `matches`, `applications`,
  `feedback`, `digest`, `jobs/{id}/ai/*`, `insights/career`, `messages`, `opportunities`,
  `workspace`, `dashboard/summary`.
- *Sistema / custo* (`RequireAuthorization("System")` = autenticado no MVP): `jobs/discover`,
  `jobs/search`, `jobs/rescore`, `discovery/*`, `companies` (mutações), `detect-ats-bulk`,
  `bacen/*`, `consulting-radar/*`, `debug/*`. Em dev pode-se relaxar com
  `Dev:OpenSystemEndpoints=true`.
- *Leitura global anônima*: `GET /api/jobs`, `GET /api/companies`.

**Endpoints de auth/workspace**

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/auth/register` | Cria usuário + workspace e já loga (cookie) |
| POST | `/api/auth/login` | Login por e-mail/senha (cookie) |
| POST | `/api/auth/logout` | Encerra a sessão |
| GET  | `/api/auth/me` | Usuário atual (`401` se anônimo) |
| GET  | `/api/workspace/me` | Workspace + `defaultCandidateProfileId` |
| GET  | `/api/admin/overview` | **Admin** — contagens, crons dos jobs, execuções recentes |
| POST | `/api/admin/sweep` | **Admin** — varredura sob demanda (`maxCompanies`, `maxDurationSeconds`), em background |
| POST | `/api/profile-imports/linkedin-pdf` | Upload do PDF do LinkedIn (multipart, ≤5 MB) → draft de perfil |
| GET  | `/api/profile-imports/{id}` | Import do próprio workspace |
| POST | `/api/profile-imports/{id}/apply` | Cria o `CandidateProfile` a partir do draft revisado (idempotente) |

**Usuário dev (somente Development ou `Dev:SeedDevUser=true`)**
No startup, em ambiente de desenvolvimento, um usuário local `dev@local` (senha `Dev:SeedPassword`,
padrão `dev12345`) é criado, e **todos os perfis pré-auth existentes são vinculados ao workspace
dele** — assim os dados de hoje continuam visíveis após o login. **Nunca** roda em produção sem a
flag explícita.

> Estado transitório: `candidate_profiles.workspace_id` é **nullable** nesta migration
> (`AddAuthWorkspace`) para permitir o backfill pelo seeder. Quando não houver mais NULLs, uma
> migration futura deve torná-la `NOT NULL`.

**Frontend**: porta de entrada com login/cadastro → onboarding curto (4 passos) que cria o primeiro
perfil → feed do perfil. O `oos.selectedProfileId` (localStorage) só é usado se pertencer ao
usuário; senão cai no perfil padrão. Logout limpa a seleção e volta ao login.

**LLM-as-judge (re-rank do top-N, sob demanda)**: o botão **"Refinar com IA"** na tela
Oportunidades chama `POST /api/matches/llm-rerank?candidateProfileId=&take=10`, que re-pontua as N
melhores vagas do perfil ativo via `understanding+fit` (LLM autoritativo, `EngineVersion=llm-fit-v2`),
com guarda de orçamento (`IQueryBudgetManager`, cost center `LlmRerank`). Sem chave de LLM responde
200 + `llmConfigured=false` (no-op transparente). Auth + workspace-scoped (perfil alheio → 403).

**Matching híbrido (semântico + heurístico, opcional)**: além do motor heurístico transparente, há uma
camada semântica por **embeddings** (`IEmbeddingProvider`: OpenAI `text-embedding-3-small` quando há
`OpenAI:ApiKey`, senão um fallback determinístico por hashing). Com `FeatureFlags:EnableSemanticMatch=true`,
o score mistura a similaridade de cosseno (perfil × vaga) com o heurístico (`Matching:SemanticWeight`,
padrão 0,4), mantendo as travas. Embeddings ficam em `JobPosting`/`CandidateProfile` (`AddEmbeddings`);
cosseno é calculado em C# (pgvector é otimização futura). Off por padrão.

**Importar perfil do LinkedIn (onboarding opcional)**: no onboarding o usuário pode enviar o **PDF
exportado do LinkedIn** em vez de preencher manualmente. O sistema extrai o texto (UglyToad.PdfPig,
Apache-2.0, sem OCR), detecta o padrão LinkedIn, parseia seções (PT/EN) e propõe um draft editável que
o usuário **revisa antes de salvar**. Determinístico primeiro; normalização por LLM é opcional
(`FeatureFlags:EnableLinkedInPdfLlmNormalization`, default off) e estritamente conservadora (nunca
inventa/altera experiências, empresas, cargos ou datas). Sem scraping, sem OAuth, sem URL — só o
arquivo enviado. Só o JSON parseado é persistido (privacidade).

**Admin (`AppUser.IsAdmin`)**: o `dev@local` é admin (seed em Development). Aba **"Operação"** no
frontend (só admin) mostra status do radar e dispara varredura sob demanda com limite de empresas/tempo
(`POST /api/admin/sweep`, em background). A captura contínua continua no Worker (`continuous-discovery`).

**Relevância (credibilidade do score)**: a detecção de stack (`StackTaxonomy`) casa por **fronteira de
palavra** (`"java"` ≠ `"javascript"`), e o **título** define a stack primária — uma vaga "C# Developer"
não vira top match de um perfil Java por uma menção incidental no corpo. A frase "por que combina" do
card reflete a stack real da vaga (não um ".NET/C#" fixo).

## Endpoints (Fase 1)

| Método | Rota | Descrição |
|--------|------|-----------|
| GET    | `/`  | Health check |
| GET    | `/api/candidate-profile` | Perfil **default/atual** (back-compat). Multi-perfil: ver `/api/candidate-profiles` na seção "Multi-perfil" |
| GET    | `/api/candidate-profile/{id}` | Perfil por id |
| POST   | `/api/candidate-profile` | Cria perfil |
| PUT    | `/api/candidate-profile/{id}` | Atualiza perfil (mantém `UpdatedAtUtc`) |
| GET    | `/api/companies` | Lista empresas (estratégicas primeiro) |
| GET    | `/api/companies/{id}` | Empresa por id |
| POST   | `/api/companies` | Cria empresa (`Priority`: 1=Low … 4=Strategic) |
| PUT    | `/api/companies/{id}` | Atualiza empresa |
| DELETE | `/api/companies/{id}` | Remove empresa |
| GET    | `/api/jobs` | Lista vagas |
| GET    | `/api/jobs/{id}` | Vaga por id |
| POST   | `/api/jobs/discover` | **Descoberta manual**: busca vagas nos providers ATS e persiste (dedup). Body opcional `{ "companyId": "..." }` |
| POST   | `/api/jobs/{id}/match` | **Análise manual (heurística)**: normaliza + calcula score e persiste o match |
| GET    | `/api/jobs/{id}/match` | Último match da vaga |
| POST   | `/api/jobs/{id}/archive` | Arquiva a vaga |
| POST   | `/api/jobs/{id}/ai/analyze` | **IA**: interpreta a vaga (LLM) + score de fit, persiste o match |
| POST   | `/api/jobs/{id}/ai/generate-outreach` | **IA**: gera drafts (LinkedIn/e-mail/carta/follow-up) como `Draft`. Bloqueado se score < 60 (422) |
| POST   | `/api/jobs/{id}/ai/suggest-cv-tailoring` | **IA**: sugestões de ajuste de CV (não altera o CV) |
| POST   | `/api/insights/career` | **IA**: insights de carreira sobre vagas analisadas. Body opcional `{ "maxJobs": 50 }` |
| GET    | `/api/opportunities` | Lista o pipeline |
| GET    | `/api/opportunities/follow-ups` | Follow-ups pendentes (vencidos) |
| GET    | `/api/opportunities/{id}` | Oportunidade por id |
| PUT    | `/api/opportunities/{id}/status` | Muda status (**manual**; única via para status pós-revisão como `SentManually`) |
| PUT    | `/api/opportunities/{id}/notes` | Atualiza notas |
| PUT    | `/api/opportunities/{id}/follow-up` | Define `NextFollowUpAtUtc` |
| POST   | `/api/companies/import-csv` | Importa empresas de um CSV (`name,websiteUrl,careersUrl,industry,country`) |
| POST   | `/api/companies/{id}/detect-ats` | Detecta o ATS (Greenhouse/Lever/Gupy/Workday) crawleando o site |
| POST   | `/api/companies/{id}/discover-website` | Descobre o site oficial a partir do nome (heurístico) |
| POST   | `/api/companies/onboard?limit=N` | Encadeia: descobre site → detecta ATS para empresas sem site (maior prioridade primeiro) |
| POST   | `/api/jobs/search` | Busca vagas por palavra-chave (Gupy); auto-cria empresas |
| GET    | `/api/recruiters` | Lista recrutadores (adicionados manualmente) |
| POST   | `/api/recruiters` | Cria recruiter lead |
| PUT    | `/api/recruiters/{id}` | Atualiza recruiter lead |
| DELETE | `/api/recruiters/{id}` | Remove recruiter lead |
| GET    | `/api/digest/preview` | Renderiza o digest (Markdown + HTML) **sem enviar**. Query opcional `?minScore=60` |
| POST   | `/api/digest/send` | Envia o digest por e-mail (registra `ExecutionRun`). Sem SMTP → não envia e avisa |

### Exemplo: rodar análise manual

```bash
# Pega uma vaga e roda o match contra um perfil (default se candidateProfileId for omitido)
curl -X POST http://localhost:5000/api/jobs/{jobId}/match
```

Resposta inclui `overallScore` (0–100), sub-scores (técnico, domínio, senioridade,
localização, idioma), `recommendation`, `strengths`, `risks` e `rationale`.

## Descoberta de vagas (Fase 2)

Providers de fontes públicas de ATS implementam `IJobSourceProvider`:

- **Greenhouse** — `GET https://boards-api.greenhouse.io/v1/boards/{token}/jobs?content=true`
  (token derivado de `boards.greenhouse.io/{token}` na `CareersUrl`).
- **Lever** — `GET https://api.lever.co/v0/postings/{handle}?mode=json`
  (handle derivado de `jobs.lever.co/{handle}`).

Habilite/desabilite via `FeatureFlags:EnableGreenhouseProvider` / `EnableLeverProvider`.
Cada provider usa `HttpClient` com timeout, `User-Agent` identificável e retry com backoff
para falhas transitórias (429/5xx).

A orquestração (`JobDiscoveryService`) é resiliente: falha de um provider/empresa é
**logada e registrada** no `ExecutionRun`, sem abortar o ciclo. A deduplicação é por
`(SourceProvider, ExternalId)` — vagas reencontradas são **atualizadas, nunca duplicadas**
(índice único no banco). Nenhuma candidatura é enviada; apenas descoberta + persistência.

O Worker agenda `discover-jobs` diariamente; também é possível disparar manualmente via
`POST /api/jobs/discover`.

### Descoberta contínua + crawler de carreiras + busca web-aberta

Além dos ATS providers, o sistema descobre vagas de forma **contínua** e **mais ampla**:

- **Descoberta contínua** — o Worker roda `continuous-discovery` a cada 15 min
  (`Jobs:ContinuousDiscoveryCron`, default `*/15 * * * *`) para manter o radar fresco
  enquanto o sistema está de pé, e `search-jobs` a cada 3 h (`Jobs:SearchCron`) para
  espalhar a cota do Google ao longo do dia.
- **Crawler genérico de carreiras** (`GenericCareersCrawler`, flag
  `FeatureFlags:EnableGenericCrawler`) — acessa o **site da empresa** direto, acha a página
  de "Carreiras/Trabalhe Conosco" e extrai vagas por regex de cargo (mesmo domínio, exclui
  hosts de ATS, `ExternalId` por hash da URL).
- **Playwright (render de SPA)** — sites de fintech modernos são SPAs que só renderizam
  vagas via JS. Quando o crawler não acha nada no HTML estático e o Chromium está instalado,
  ele renderiza a página com Playwright (`IPageRenderer`/`PlaywrightPageRenderer`) e tenta de
  novo. **Degrada graciosamente**: sem o browser, cai no fetch HTTP e loga a dica de
  instalação. Instale uma vez:

  ```bash
  # após o build (Microsoft.Playwright vive no projeto Infrastructure; o script
  # é copiado para o output de quem o referencia — Api/Worker):
  pwsh src/OpportunityOS.Worker/bin/Debug/net10.0/playwright.ps1 install chromium
  ```

- **Busca web-aberta via Serper.dev** (`SerperWebJobSearchProvider`, flag
  `FeatureFlags:EnableSerperWebSearch`) — busca **.NET/C# recentes na web inteira** (índice
  Google real), não só nos ATS conhecidos. Cada resultado orgânico vira oportunidade; a
  empresa é derivada do host; agregadores (LinkedIn/Indeed/Glassdoor/ZipRecruiter/…) são
  filtrados. Frescor via `tbs=qdr:m` (último mês, configurável em `Search:SerperFreshness`).
  O tier grátis limita rajadas, então há um `Search:SerperDelayMs` (default 1200ms) entre
  queries e um teto `Search:SerperMaxQueriesPerCall` (default 4) para preservar os créditos.

  > **Requer `Search:SerperApiKey`** nos user-secrets. Sem ela o provider fica **dormente**
  > (não é registrado). Configure:
  > ```bash
  > dotnet user-secrets set "Search:SerperApiKey" "<sua-chave>" --project src/OpportunityOS.Api
  > dotnet user-secrets set "Search:SerperApiKey" "<sua-chave>" --project src/OpportunityOS.Worker
  > ```
  >
  > **Por que não o Google CSE?** A JSON API do Google **não serve mais engines whole-web**
  > (restrição de jan/2026): mesmo com chave e projeto corretos, um `cx` de web-aberta
  > retorna `403 "This project does not have the access to Custom Search JSON API"`. O
  > `GoogleWebJobSearchProvider` segue no código (flag `EnableGoogleWebSearch`) só funciona com
  > um `cx` **escopado a sites**; para web-aberta de verdade, use o Serper.

- **Auto-score na descoberta** — todo job recém-descoberto (qualquer provider) é pontuado
  na hora contra o **perfil default** pelo **match engine heurístico** (sem LLM, barato), criando
  um `OpportunityMatch`. É isso que faz a vaga **aparecer na tela** sem passo manual. Os demais
  perfis recebem matches via `POST /api/jobs/rescore` (por perfil ou `allProfiles=true`). Jobs
  antigos sem match são pontuados quando reencontrados (backfill). Falha de score nunca
  interrompe a descoberta.

- **Enriquecimento de snippet** (`IJobContentEnricher`/`HtmlJobContentEnricher`) — vagas da
  web aberta vêm com só o snippet da busca; antes de pontuar, o sistema **busca o texto real
  da página** (HTTP GET + strip de tags; fallback Playwright para SPA) para o heurístico ter
  material e não subestimar boas vagas. Limitado por run (8) e só para descrições curtas
  (<300 chars), pra não martelar sites na descoberta contínua.

- **Ranking frescor-primeiro + validação de links** — o feed (`/api/matches`) descarta
  postagens publicadas há mais de `maxAgeDays` (default 120, provavelmente fechadas) e dá
  bônus de frescor na ordenação (recentes sobem); o `ValidateLinksJob` (cron
  `Jobs:ValidateLinksCron`) faz HEAD-check e expira 404/410. Os contadores do dashboard
  ("fortes 75+") respeitam o mesmo filtro, então o número não inclui vagas mortas.

- **"Analisar com IA" sob demanda** — quando o rationale é o do heurístico (seco), um clique
  chama `/ai/analyze` (LLM) e troca por um "por que combina" em prosa + score refinado, sem
  custo de LLM até você pedir.

## Firehose — descoberta massiva (camada de coleta)

Nova arquitetura em 3 camadas: **Firehose** (coleta tudo, com ruído) → **Qualified** (triado)
→ **Action Today** (poucas acionáveis). Tese: *coletar muito, organizar o caos, classificar a
qualidade, agir seletivamente.* O Firehose **não usa LLM** e **salva o resultado bruto antes de
filtro forte** — não descarta por score baixo.

- **`SearchCampaign`** — campanha nomeada (keywords-semente, fontes-alvo, domínios excluídos,
  budget de queries). **`SearchQueryTemplate`** — templates com placeholders. **`SearchQueryExecution`** —
  auditoria de cada query (resultados, novos, duplicados). **`RawJobCandidate`** — candidato bruto
  com `SourceType`, `SourceConfidenceScore`, `RequiresManualValidation`, fingerprint, etc.
- **`QueryExpansionService`** — combina dimensões (role × stack × work-mode × domínio) e aplica
  filtros `site:` para gerar **centenas de queries** a partir de poucas sementes. Sem LLM.
- **`FirehoseService`** + **`IRawSearchProvider`** (`SerperRawSearchProvider`, query verbatim,
  paginação de 10/req pois Serper rejeita `num>10`) — roda as queries, classifica a fonte por host
  (ATS oficial / job board / agregador / social-indexed / search result), **dedup por URL**, salva
  todo resultado como `RawJobCandidate` e audita tudo (`SearchQueryExecution` + `ExecutionRun`).
- **LinkedIn indexado** = `SocialIndexed`, **revisão manual obrigatória** (sem scrape, sem login,
  sem automação). Agregadores aparecem, mas marcados.

Endpoints (`/api/discovery`): `POST /campaigns` · `GET /campaigns` · `GET /campaigns/{id}` ·
`POST /campaigns/{id}/run` · `POST /quick-search` · `POST /aggressive-search` · `GET /raw-candidates`.

```bash
# coleta massiva (gera 100+ queries, salva centenas de candidatos brutos)
curl -X POST localhost:5077/api/discovery/aggressive-search \
  -H "Content-Type: application/json" \
  -d '{"maxQueries":200,"maxResultsPerQuery":10,"saveRawCandidates":true}'
curl "localhost:5077/api/discovery/raw-candidates?take=200"   # ver o volume bruto
```

> Requer `Search:SerperApiKey` (mesma chave do fluxo qualificado). O fluxo antigo de matches/
> oportunidades segue intacto — o Firehose é uma camada nova e aditiva.

**Qualidade de fonte (`ISourceClassifierService`)** — toda fonte é classificada (não descartada):
ATS oficial 90 · Gupy 80 · página de carreira 90 · job board 70 · busca web 50 · agregador 35 ·
LinkedIn/social 25 (**revisão manual obrigatória**) · snippet sem empresa 15. O mesmo classificador
roda no fluxo qualificado (`JobPosting` ganhou `SourceType`/`SourceConfidenceScore`/`RequiresManualValidation`/
`SourceName`/`RealCompanyName`/`OriginalJobUrl`).

**Budget (`IQueryBudgetManager`, seção `DiscoveryBudget`)** — teto diário por provider (Serper
1000/dia, etc.), reset UTC. Quando esgota, a busca agressiva **para com `CompletedWithBudgetLimit`**
(nunca quebra em silêncio). Protege a cota antes das varreduras massivas.

**Empresa real × fonte (`ICompanyNameResolver`)** — extrai a empresa contratante do título/host
(ex.: `"... at MARGO - Jobgether"` → MARGO via Jobgether; `"[FORTIS SRT] ..."` → FORTIS SRT). Host
de agregador **nunca** vira empresa sem evidência; sem empresa clara → "empresa não confirmada".
(Fallback LLM previsto, adiado por custo.)

**Dedup semântica (`IJobFingerprintService` + `JobPostingSourceOccurrence`)** — fingerprint
estável (título+empresa+localização+senioridade+skills+hash da descrição). A mesma vaga em fontes
diferentes é marcada `Duplicate` (continua visível como ocorrência, não some do volume); a melhor
fonte fica como principal na promoção. `RawJobCandidate.NormalizedFingerprint` indexado.

**Promoção (`IRawCandidatePromotionService`)** — a ponte Firehose → fluxo qualificado:
`POST /api/discovery/raw-candidates/{id}/promote` e `POST /api/discovery/promote-batch`. Cria
`JobPosting` (com fingerprint + qualidade de fonte) só quando há **empresa confirmada** (agregador
sem empresa não vira Company); dedup por fingerprint vira `JobPostingSourceOccurrence` na vaga
existente (melhor fonte como principal); pontua heurístico para já aparecer no feed. Sem LLM.

**Ranking (`IDiscoveryRankService`)** — dois scores: `FitScore` (relevância) e `DiscoveryRank`
(Fit·0.50 + SourceConfidence·0.20 + Freshness·0.15 + CompanyPriority·0.10 + Feedback·0.05).
`GET /api/matches?sort=rank` ordena pelo DiscoveryRank (base do Qualified view).

**Feedback + métricas** — `POST /api/feedback` (`UserFeedback`: relevante/irrelevante/empresa
errada/duplicada/já apliquei/…) persistido para métricas. `GET /api/discovery/metrics` (volume
hoje/semana, queries, promovidas, taxa de dedup, confiança/fit médios, acionáveis, por fonte) e
`GET /api/discovery/provider-quality` (por provider: queries, brutos, promovidos, dup-rate, confiança).

**IA depois da triagem (P10)** — o Firehose **nunca** usa LLM. A análise LLM automática só roda
na descoberta qualificada e só quando: heurística ≥ 65 **e** `SourceConfidence` ≥ 40 **e** dentro
do **budget diário de LLM** (`DiscoveryBudget:LlmDailyAutoAnalyses`, cost-center "LlmAuto" no
`QueryBudgetManager`) **e** sob o teto por run. O resto fica sob demanda (`/ai/analyze`,
"Analisar com IA" = user-requested), com os prompts sempre auditados em `PromptExecutionLog`.

**3 telas (P8)** — Action Today (Fit≥75, acionáveis), Qualified (`sort=rank` por DiscoveryRank),
Firehose (RawJobCandidate com `SourceBadge` "Empresa X via Fonte Y" + métricas + Promover + feedback).

**Bacen Financial Sweep (P2)** — usa instituições financeiras promovidas (Source=Bacen) como
empresas-alvo de varredura de vagas .NET/C#. `GET /api/discovery/bacen-financial-sweep/preview`
(quantas por prioridade, queries estimadas, custo) e `POST /api/discovery/bacen-financial-sweep`
(`minimumPriority` default High, exclui cooperativas por padrão). Sem LLM; Bacen é fonte de
empresas, não de vagas. Reusa `SweepCompaniesAsync` (queries por empresa, budget-guarded).

**Novos providers (P13)** — por decisão da própria spec ("só depois da base"), os platforms
extras (Workday, Teamtailor, Recruitee, Workable, Breezy, Programathor, GeekHunter, Coodesh,
Remotar, APInfo, Trampos) são alcançados **via Firehose** (filtros `site:` em `QueryExpansionService`)
e classificados pelo `SourceClassifier` (ATS hosts conhecidos). Parsers dedicados por plataforma
(`IJobSourceProvider`) ficam para depois, guiados pelo Provider Quality Dashboard (P12) — cada um
exigindo SourceConfidence, dedup, rate-limit, fixture e métricas.

**Consulting Radar (P3)** — descobre consultorias/software houses automaticamente
(`ConsultingCompanyCandidate`): roda queries de consultoria, deriva candidatos do host, pontua por
sinais explicáveis (`ConsultingSignals`: +consultoria/+outsourcing/+transformação/+carreiras/
+.NET/−SaaS/−sem-site…) e semeia uma lista conhecida (GFT, Stefanini, CI&T, Zup…). Promove a
Company (Source=SearchDiscovery, tags consulting/outsourcing/software-house, prioridade por
confiança) **só com confiança ≥ 70**. Endpoints `/api/consulting-radar/*`.

### Descoberta automática (Worker)

O Worker mantém o Firehose se enchendo sozinho (B1), tudo budget-guarded:
- **`firehose-sweep`** (`Jobs:FirehoseCron`, default `0 */4 * * *`) — busca ampla periódica.
- **`promote-candidates`** (`Jobs:PromotionCron`, default `30 */4 * * *`) — promove candidatos com
  empresa confirmada para o feed qualificado (heurístico, sem LLM).
- **`bacen-financial-sweep`** e **`consulting-radar`** — varreduras caras, **opt-in** via
  `FeatureFlags:EnableBacenSweepJob` / `EnableConsultingRadarJob` (off por padrão), crons próprios.

Quando o budget diário esgota, a varredura para com `CompletedWithBudgetLimit` — nunca estoura a cota.

## Empresas observadas no LinkedIn (seed interno do radar)

**Objetivo:** aumentar a cobertura do radar com empresas que o usuário **observou manualmente**
no LinkedIn (consultorias, fintechs, marketplaces remotos, software houses). **Não é integração
com LinkedIn, não é feature de usuário, não é lista de candidaturas** — é só um seed interno que
alimenta a entidade `Company` existente para o fluxo atual alcançar (Company → Website Discovery →
Career Page → ATS Detection → Job Discovery).

- **`ObservedCompaniesSeed`** (Infrastructure) — lista classificada (financeira/consultoria/
  marketplace/produto) com prioridade e tags. Idempotente: **não duplica** (dedup por nome
  normalizado, ignorando sufixos Inc./Ltd/LTDA/S.A./Oficial/Brasil), **não sobrescreve**
  WebsiteUrl/CareersUrl já preenchidos, só **mescla tags** e **eleva** prioridade (nunca rebaixa).
  Marca `needs-website-discovery`/`needs-ats-detection` quando faltam. Tags globais:
  `observed-linkedin`, `manual-radar-seed`. Registra `ExecutionRun` (`ObservedCompaniesSeed`).
- **Como rodar:** `POST /api/companies/seed-observed` (comando dev interno; retorna o resumo).
  Depois, o fluxo existente descobre site/ATS: `POST /api/companies/backfill-websites`,
  `POST /api/companies/{id}/discover-website`, `POST /api/companies/{id}/detect-ats`,
  `POST /api/companies/onboard`, e então o Job Discovery busca vagas nessas empresas.
- **Limitações honestas:** o seed só prepara o radar — não busca vagas na mesma transação;
  agregadores (Indeed/Glassdoor/SimplyHired/Jobgether/…) **não** entram como empresa estratégica;
  a descoberta de site é heurística (acerta a maioria das marcas conhecidas, erra domínios
  atípicos); ATS costuma estar em `/careers`, então quem fecha o ciclo é o crawler no job discovery.
- **Denylist:** job boards/agregadores (Indeed, Glassdoor, SimplyHired, Remotejobs, Jobbol, LinkedIn Jobs,
  Code Vagas, JobJá, Dev Life, Netvagas, Vagas PJ) e perfis pessoais/recrutadores **nunca** viram Company
  contratante. Se já existirem no banco, são rebaixados para `Low` com `source-only`/`noisy-source`/`do-not-promote`.
- **Regra inviolável:** o sistema **nunca** acessa/raspa/loga no LinkedIn, nem aplica para vagas.

## AI Copilot Layer (Fase 3)

Camada explícita de IA que **interpreta, analisa e redige** — mantendo ações externas
sob controle do sistema e revisão humana. Serviços (`Application/AI`):

1. **JobUnderstandingService** — extrai skills, domínio, senioridade, modelo, idioma, responsabilidades e riscos da vaga.
2. **CandidateFitAnalysisService** — compara vaga × perfil e produz um `OpportunityMatch` (scores + recomendação).
3. **OutreachDraftService** — gera mensagem LinkedIn, e-mail, carta e follow-up (sempre `Draft`).
4. **CvTailoringSuggestionService** — sugere ajustes de CV (**nunca** altera o CV).
5. **CareerInsightService** — agrega padrões entre várias vagas (tecnologias pedidas, lacunas, domínios, ideias de estudo/posts).

A IA **não** busca vagas sozinha, não envia mensagens, não aplica, não altera o CV e
não inventa experiências.

**Provider de LLM** (`ILlmProvider`) — selecionado por configuração:
- `Llm:Provider` aceita `auto` (padrão), `Anthropic`, `OpenAI` ou `Fake`.
- Em `auto`: usa `AnthropicLlmProvider` se houver `Anthropic:ApiKey`; senão `OpenAiLlmProvider` se houver `OpenAI:ApiKey`; senão `FakeLlmProvider`.
- `FakeLlmProvider` retorna JSON determinístico (funciona **offline**, sem chave).
- Com `FeatureFlags:EnableLlmAnalysis=false` → provider reporta não-configurado e os serviços usam **fallback heurístico**.

> **Chaves de API nunca vão no repositório.** Configure via User Secrets ou variável de
> ambiente, definidas por você:
> ```bash
> cd src/OpportunityOS.Api
> dotnet user-secrets set "Anthropic:ApiKey" "<sua-chave>"   # Claude (Messages API)
> # ou
> dotnet user-secrets set "OpenAI:ApiKey" "<sua-chave>"
> ```

**Garantias** (todas testadas):
- Toda execução é auditada em `prompt_execution_logs` (`promptVersion`, `modelName`, `rawResponse`, `success`, `usedFallback`, `createdAtUtc`).
- JSON inválido / falha de chamada → registra erro e **cai no fallback heurístico** sem quebrar o fluxo.
- Não gera outreach se o score for < 60 (HTTP 422).
- Prompts versionados em `Application/AI/Prompts.cs`.

## CRM de oportunidades (Fase 4)

Quando um match atinge **score ≥ 70**, o sistema cria automaticamente uma `Opportunity`
(status `Analyzed`). Quando um outreach é gerado, a oportunidade avança para
`ReadyForHumanReview`.

**Regra central**: o sistema só pode avançar o status **automaticamente até
`ReadyForHumanReview`**. Qualquer estado além disso (`SentManually`, `AppliedManually`,
`InterviewScheduled`, …) exige **ação humana** via `PUT /api/opportunities/{id}/status`
(o domínio lança exceção se o sistema tentar fazer isso sozinho).

`RecruiterLead` guarda contatos **adicionados manualmente** pelo usuário — o sistema
nunca raspa o LinkedIn. Follow-ups: defina `NextFollowUpAtUtc` e consulte os pendentes
em `GET /api/opportunities/follow-ups`.

## Email Digest (Fase 5)

Resumo das melhores oportunidades (score ≥ 60 por padrão), ordenadas por score, com
empresa, vaga, link, recomendação, pontos fortes, riscos e — quando houver — mensagem
curta, carta e notas de CV.

- `GET /api/digest/preview` renderiza Markdown + HTML **sem enviar**.
- `POST /api/digest/send` envia via SMTP e registra um `ExecutionRun`. O `SendDailyDigestJob`
  (Worker) agenda o envio diário (`Jobs:DailyDigestCron`, padrão `0 9 * * *`).
- **Sem oportunidades relevantes** → registra o `ExecutionRun` sem erro e **não envia**.
- **Sem SMTP configurado** → não envia e retorna aviso (o preview continua funcionando).
- Sem anexos; nenhuma candidatura é enviada; o envio só ocorre quando o endpoint/job é acionado.

**Configuração SMTP** (via User Secrets — nunca no repositório):
```bash
cd src/OpportunityOS.Api      # e idem em src/OpportunityOS.Worker p/ o job diário
dotnet user-secrets set "Email:Smtp:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:Smtp:Port" "587"
dotnet user-secrets set "Email:Smtp:Username" "voce@gmail.com"
dotnet user-secrets set "Email:Smtp:Password" "<Gmail App Password (requer 2FA)>"
```

## Bacen Pix Importer (radar de empresas)

Popula o **radar inicial de empresas** a partir da **lista oficial de participantes do
Pix do Banco Central** (CSV). É um processo de **dois estágios** para o radar não virar
"lista gigante suja":

1. **Import** → grava tudo em `BacenInstitution` (staging cru). Fonte = CSV oficial,
   Latin1 / `;`-separado, **URL configurável** em `Bacen:PixParticipantsCsvUrl`
   (sem scraping de HTML, sem adivinhar endpoint).
2. **Promote** → cria/atualiza `Company` **apenas para instituições elegíveis** (autorizadas
   pelo BCB e do tipo Instituição de Pagamento / Banco / Sociedade de Crédito Direto / SCFI),
   com prioridade calculada (Strategic/High/Medium/Low) e tags herdadas + `company-radar`.
   Cooperativas e não-autorizadas ficam de fora do radar.

> O Bacen Importer **não busca vagas** — só monta o radar de empresas. A busca de vagas
> continua nos ATS providers (Greenhouse/Lever/Gupy) + detecção de ATS / página de carreiras.

**Encadeando o funil (onboarding):** as empresas promovidas vêm sem board. `POST
/api/companies/onboard` acha o **board de vagas (ATS)** de cada uma e grava em `CareersUrl`.
Duas estratégias por trás de `IAtsBoardFinder`:

- **Google CSE** (recomendado): funciona com engine **escopado a ATS** (domínios
  greenhouse.io/lever.co/gupy.io/inhire.app/…) **ou** com engine **de web aberta**
  (grandfathered até 2027). `GoogleAtsBoardFinder` faz: pass 1 — se o resultado já é um
  board de ATS, classifica e retorna; pass 2 — senão usa o 1º resultado orgânico como site
  oficial e crawleia atrás do ATS (pula LinkedIn/agregadores). Configure `Search:ApiKey` +
  `Search:SearchEngineId`.
  > Nota: o Google descontinuou "buscar em toda a web" para engines **novos** em 20/01/2026;
  > engines antigos com a opção ligada seguem válidos até 2027. Ambos funcionam aqui.
- **Heurístico (fallback)**: descobre o site oficial pelo nome (deriva domínios, valida a
  marca — conservador) e crawleia em busca do ATS.

O `AtsDetector` reconhece Greenhouse, Lever, Gupy, Workday, Ashby, SmartRecruiters,
Workable, Recruitee, Teamtailor, Breezy, inhire, Abler, Solides, Pandapé, Kenoby, Quickin,
JobConvo, Taqe, 99jobs, Recrutei, GeekHunter, Coodesh, Programathor — e
**Greenhouse/Lever/Gupy/SmartRecruiters/Ashby** têm provider de **busca de vagas** (fetch);
os demais são detectados/linkados até existir um provider.

**Validação real:** 919 instituições importadas; 279 promovidas a empresas; 640 filtradas.

Endpoints:

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/bacen/pix-participants/import` | Baixa o CSV oficial → `BacenInstitution` (idempotente por CNPJ, fallback ISPB) |
| POST | `/api/bacen/pix-participants/promote-to-companies` | Promove elegíveis → `Company` (idempotente por nome) |
| GET  | `/api/bacen/pix-participants` | Lista (filtros: `institutionType`, `authorizedByBacen`, `tag`, `search`) |
| GET  | `/api/bacen/pix-participants/{id}` | Detalhe |

Como rodar:

```bash
docker compose up -d postgres
dotnet run --project src/OpportunityOS.Api          # aplica migrations no startup
curl -X POST http://localhost:5000/api/bacen/pix-participants/import
curl -X POST http://localhost:5000/api/bacen/pix-participants/promote-to-companies
```

Limitações conhecidas: a URL do CSV é um snapshot fixo (não há descoberta automática da
versão mais recente); a descoberta nome → site/carreiras é responsabilidade de outro módulo
(`detect-ats` / futuro `CompanyWebsiteDiscoveryService`); cada operação registra um
`ExecutionRun` (`BacenPixParticipantsImport` / `BacenPixParticipantsPromotion`).

## Dashboard (frontend)

Dashboard mínimo em **React + Vite + TypeScript** (`frontend/`) consumindo a API:
abas **Overview** (cards: empresas, vagas, oportunidades, follow-ups), **Empresas**,
**Vagas**, **Oportunidades** e **Digest** (renderiza o HTML do preview).

```bash
docker compose up -d postgres
dotnet run --project src/OpportunityOS.Api     # API em http://localhost:5077
cd frontend && npm install && npm run dev      # http://localhost:5173
```

O dev server faz proxy de `/api` para a API (sem CORS em dev). Se a API estiver em
outra porta (ex.: `5000` no docker compose), use
`VITE_API_TARGET=http://localhost:5000 npm run dev`.

## Multi-perfil de candidato (entidades globais vs por perfil)

O sistema pontua oportunidades para **vários perfis de candidato** (ex.: Backend .NET,
Java, Frontend React, Data Engineer). A descoberta continua **global** — uma vaga **não** é
duplicada por perfil; o que muda por perfil é o **score, o feedback, a oportunidade, o
rascunho e a aplicação**.

| Global (uma vez para todos) | Por perfil (`CandidateProfileId`) |
|-----------------------------|-----------------------------------|
| `Company`, `JobPosting`, `RawJobCandidate` | `OpportunityMatch` |
| `SearchCampaign`, `SearchQueryExecution` | `Opportunity` |
| `JobPostingSourceOccurrence` | `GeneratedMessage` |
| `BacenInstitution`, `ConsultingCompanyCandidate` | `UserFeedback` / Applications |

**Resolução do perfil atual** — `ICurrentCandidateProfileProvider`
(`src/OpportunityOS.Application/Profiles/`): `id explícito → IsDefault → mais recente`. Não
há estado global mutável de “perfil ativo”; a seleção vai **explícita** em cada request
(`?candidateProfileId=…`) e o front lembra a escolha em `localStorage`. Isso prepara o
caminho para auth/multi-tenancy: o mesmo ponto passa a resolver pelo usuário autenticado.

**Criar / alternar perfil**

```bash
# Listar perfis
curl localhost:5077/api/candidate-profiles

# Criar um perfil
curl -X POST localhost:5077/api/candidate-profiles -H "Content-Type: application/json" -d '{
  "fullName":"Java Dev","displayName":"Java Backend","headline":"Backend Java/Spring",
  "summary":"...","location":"Brasil","seniority":"Pleno/Sênior","preferredLanguage":"pt-BR",
  "coreSkills":["Java","Spring Boot","Kafka","AWS","PostgreSQL"]
}'

# Mover a âncora padrão (fallback de compatibilidade)
curl -X POST localhost:5077/api/candidate-profiles/{id}/set-default
```

No frontend, o seletor no topo da sidebar troca o perfil; o feed, as aplicações e a copy
recarregam para o perfil escolhido.

**Rodar o match por perfil**

```bash
# Feed de um perfil específico
curl "localhost:5077/api/matches?candidateProfileId={id}&minScore=60"

# Re-pontuar — modos (seguro por padrão: só 1 perfil)
curl -X POST "localhost:5077/api/jobs/rescore?candidateProfileId={id}"   # só esse perfil
curl -X POST "localhost:5077/api/jobs/rescore"                            # só o perfil default
curl -X POST "localhost:5077/api/jobs/rescore?allProfiles=true"           # todos os perfis (pesado)

# Parâmetros opcionais de custo/controle:
#   take=500                          → limita o nº de vagas (mais recentes primeiro)
#   minCreatedAtUtc=2026-05-01        → só vagas descobertas a partir da data
#   engineVersion=heuristic-v2        → tag gravada nos matches produzidos
#   onlyWithoutCurrentEngineVersion=true → pula (vaga,perfil) cujo último match já é dessa versão
curl -X POST "localhost:5077/api/jobs/rescore?allProfiles=true&take=1000&onlyWithoutCurrentEngineVersion=true"

# Match sob demanda de uma vaga para um perfil
curl -X POST "localhost:5077/api/jobs/{jobId}/match?candidateProfileId={id}"

# Validação (dev): comparar o score da MESMA vaga entre todos os perfis
curl "localhost:5077/api/debug/job/{jobId}/profile-scores"
```

**Quando usar cada modo de rescore:** use `candidateProfileId` ao ajustar/criar **um** perfil;
use `allProfiles=true` (idealmente com `take`/`onlyWithoutCurrentEngineVersion`) após mudar o
**motor** ou seedar perfis novos; sem parâmetros, reprocessa apenas o default.

**Projeção `LatestOpportunityMatch` (performance):** `OpportunityMatch` é append-only, então o feed
e o digest leem a tabela‑cache `latest_opportunity_matches` (último match por `JobPostingId +
CandidateProfileId`), filtrável por perfil + score **no banco** — em vez de carregar todos os
matches em memória. Ela é mantida em sincronia a cada match criado e pode ser reconstruída:

```bash
# Reconstrói a projeção a partir dos matches existentes (idempotente; roda também no startup)
curl -X POST "localhost:5077/api/jobs/rebuild-latest-matches"
# -> { "processedPairs": ..., "created": ..., "updated": ..., "skipped": ... }
```

> Fase atual: **multi-perfil sem auth**. As próximas fases (Auth com `AppUser`/Identity e
> depois SaaS/Tenant/billing) plugam em cima do `ICurrentCandidateProfileProvider` e do
> `CandidateProfileId` já presentes nas entidades por perfil.

## Match Engine (heurístico, v2 — dirigido pelo perfil)

Sem LLM nesta fase — o score é **transparente e explicável**. As famílias de stack ficam em
`src/OpportunityOS.Application/Matching/StackTaxonomy.cs` e o motor em
`HeuristicMatchEngine.cs`. O **TechnicalFit** compara a vaga com a stack do **perfil**
(core/secondary/excluded), em vez de um viés fixo em .NET.

```
OverallScore = Técnico*0.50 + Cargo*0.15 + Senioridade*0.10 + Domínio*0.10 + Localização*0.10 + Idioma*0.05
```

Gates: `Técnico < 35 → ≤ 45`; cargo de gestão/negócio sem sinal técnico → `≤ 40`; vaga fora
da stack core do perfil → `≤ 70`. Faixas de recomendação:

| Score | Recomendação |
|-------|--------------|
| 0–39  | Ignore |
| 40–59 | SaveForLater |
| 60–74 | Apply |
| 75–89 | Prioritize |
| 90–100| Strategic |

## Testes

```bash
# Tudo
dotnet test

# Apenas unitários (não precisam de banco)
dotnet test tests/OpportunityOS.UnitTests
```

Os **testes de integração** sobem a API real contra PostgreSQL. Eles fazem **auto-skip**
quando nenhum banco está acessível. Para rodá-los:

```bash
docker compose up -d postgres
# Opcional: aponte para outro banco de teste
# export POSTGRES_TEST_CONNECTION="Host=localhost;Port=5432;Database=opportunity_os_tests;Username=postgres;Password=postgres"
dotnet test tests/OpportunityOS.IntegrationTests
```

## Migrations

```bash
# Criar uma nova migration
dotnet ef migrations add <Nome> \
  --project src/OpportunityOS.Infrastructure \
  --startup-project src/OpportunityOS.Api \
  --output-dir Persistence/Migrations

# Aplicar manualmente (a API também aplica no startup)
dotnet ef database update \
  --project src/OpportunityOS.Infrastructure \
  --startup-project src/OpportunityOS.Api
```

## Configuração / Secrets

`src/OpportunityOS.Api/appsettings.json` traz a connection string e placeholders para
OpenAI, e-mail (SMTP), Google Search e feature flags. **Não commite** chaves de API,
senhas SMTP, credenciais Gmail OAuth ou senhas de produção — use User Secrets ou variáveis
de ambiente.

## Status do roadmap

- ✅ **Fase 1 — Core funcional**: solution, projetos, PostgreSQL + EF Core, entidades,
  migrations, CRUD de CandidateProfile e Company, listagem de JobPostings, Match Engine
  heurístico v1, análise manual via API, testes.
- ✅ **Fase 2 — Automação diária**: `OpportunityOS.Worker` com Hangfire, `IJobSourceProvider`
  + providers Greenhouse e Lever, `POST /api/jobs/discover`, deduplicação por
  `(SourceProvider, ExternalId)`, `ExecutionRun` para auditoria, logs estruturados, testes
  com fake HTTP handler.
- ✅ **Fase 3 — AI Copilot Layer**: `ILlmProvider` (`OpenAiLlmProvider` + `FakeLlmProvider`),
  5 serviços de IA, prompts versionados, `GeneratedMessage`, `PromptExecutionLog` (auditoria),
  endpoints de IA, fallback heurístico e validação de JSON, testes com `FakeLlmProvider`.
- ✅ **Fase 4 — CRM de oportunidades**: entidades `Opportunity` e `RecruiterLead`,
  criação automática de oportunidade quando score ≥ 70, status manual (gate de revisão
  humana), `NextFollowUpAtUtc` + follow-ups pendentes, CRUD de recruiter leads, testes.
- ✅ **Fase 5 — Email Digest**: `IEmailDigestService` + SMTP, `GET /api/digest/preview`,
  `POST /api/digest/send`, template Markdown/HTML, `SendDailyDigestJob` (cron diário),
  skip sem oportunidades/sem SMTP, testes com fake sender.

**Roadmap planejado concluído.** Próximas ideias (fora do roadmap original): dashboard
React, integração Banco Central / participantes Pix, Google Custom Search, crawler
Playwright para páginas com JS, e tailoring de CV em PDF/DOCX.
