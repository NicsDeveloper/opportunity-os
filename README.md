# Opportunity OS

Sistema pessoal de inteligência de oportunidades profissionais: mapeia empresas-alvo,
descobre vagas públicas, mede aderência ao perfil do candidato, gera abordagens
revisáveis e envia um digest por e-mail para **revisão humana**.

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

**Encadeando o funil (onboarding):** as empresas promovidas vêm sem site. `POST
/api/companies/onboard` descobre o **site oficial** (heurístico: deriva domínios do nome,
valida que a página menciona a marca — conservador, prefere errar para menos a apontar
errado) e em seguida roda o **detector de ATS**. Para recall maior, dá para plugar Google
Custom Search atrás de `ICompanyWebsiteDiscoverer` quando houver `Search:ApiKey`.

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
- ✅ **Fase 4 — CRM de oportunidades**: entidades `Opportunity` e `RecruiterLead`,
  criação automática de oportunidade quando score ≥ 70, status manual (gate de revisão
  humana), `NextFollowUpAtUtc` + follow-ups pendentes, CRUD de recruiter leads, testes.
- ✅ **Fase 5 — Email Digest**: `IEmailDigestService` + SMTP, `GET /api/digest/preview`,
  `POST /api/digest/send`, template Markdown/HTML, `SendDailyDigestJob` (cron diário),
  skip sem oportunidades/sem SMTP, testes com fake sender.

**Roadmap planejado concluído.** Próximas ideias (fora do roadmap original): dashboard
React, integração Banco Central / participantes Pix, Google Custom Search, crawler
Playwright para páginas com JS, e tailoring de CV em PDF/DOCX.
