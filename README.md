# Opportunity OS

Sistema pessoal de inteligência de oportunidades profissionais: mapeia empresas-alvo,
descobre vagas públicas, mede aderência ao perfil do candidato, gera abordagens
revisáveis e envia um digest por e-mail para **revisão humana**.

> **Não** automatiza LinkedIn, **não** faz scraping logado, **não** aplica para vagas e
> **não** envia mensagens sem revisão humana. É um copiloto de carreira, não um robô de spam.

Este repositório está sendo construído por fases (Spec-Driven Development). **Esta entrega
cobre as Fases 1 (Core funcional), 2 (Automação diária / descoberta) e 3 (AI Copilot Layer).**

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

## Endpoints (Fase 1)

| Método | Rota | Descrição |
|--------|------|-----------|
| GET    | `/`  | Health check |
| GET    | `/api/candidate-profile` | Perfil ativo (mais recente) |
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

### Exemplo: rodar análise manual

```bash
# Pega uma vaga e roda o match contra o perfil ativo
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

**Provider de LLM** (`ILlmProvider`):
- Com `OpenAI:ApiKey` configurada e `FeatureFlags:EnableLlmAnalysis=true` → `OpenAiLlmProvider`.
- Sem API key → `FakeLlmProvider` (JSON determinístico, funciona offline).
- Com `EnableLlmAnalysis=false` → provider reporta não-configurado e os serviços usam **fallback heurístico**.

**Garantias** (todas testadas):
- Toda execução é auditada em `prompt_execution_logs` (`promptVersion`, `modelName`, `rawResponse`, `success`, `usedFallback`, `createdAtUtc`).
- JSON inválido / falha de chamada → registra erro e **cai no fallback heurístico** sem quebrar o fluxo.
- Não gera outreach se o score for < 60 (HTTP 422).
- Prompts versionados em `Application/AI/Prompts.cs`.

## Match Engine (heurístico, v1)

Sem LLM nesta fase — o score é **transparente e explicável** (ver
`src/OpportunityOS.Application/Matching/KnownTerms.cs`).

```
OverallScore = Técnico*0.35 + Domínio*0.25 + Senioridade*0.15 + Localização*0.15 + Idioma*0.10
```

Faixas de recomendação:

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
- ⏳ Fase 4 — Pipeline de oportunidades + recruiter leads.
- ⏳ Fase 5 — Digest por e-mail.

## Próximos passos sugeridos (Fase 4)

1. Entidades `Opportunity` e `RecruiterLead` + status manual do pipeline.
2. Criação automática de `Opportunity` quando match score >= 70.
3. `NextFollowUpAtUtc` + consulta de follow-ups pendentes.
4. Sistema nunca muda status para `SentManually` automaticamente.
