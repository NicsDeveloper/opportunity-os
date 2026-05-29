# Opportunity OS

Sistema pessoal de inteligência de oportunidades profissionais: mapeia empresas-alvo,
descobre vagas públicas, mede aderência ao perfil do candidato, gera abordagens
revisáveis e envia um digest por e-mail para **revisão humana**.

> **Não** automatiza LinkedIn, **não** faz scraping logado, **não** aplica para vagas e
> **não** envia mensagens sem revisão humana. É um copiloto de carreira, não um robô de spam.

Este repositório está sendo construído por fases (Spec-Driven Development). **Esta entrega
cobre a Fase 1 — Core funcional.**

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
  OpportunityOS.Application      # Casos de uso: Match Engine, normalização, interfaces
  OpportunityOS.Infrastructure   # EF Core, DbContext, migrations, DI
  OpportunityOS.Api              # Minimal API (endpoints), seed, OpenAPI
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

3. **(Opcional) Suba tudo via Docker Compose** (API + banco):

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
| POST   | `/api/jobs/{id}/match` | **Análise manual**: normaliza + calcula score e persiste o match |
| GET    | `/api/jobs/{id}/match` | Último match da vaga |
| POST   | `/api/jobs/{id}/archive` | Arquiva a vaga |

### Exemplo: rodar análise manual

```bash
# Pega uma vaga e roda o match contra o perfil ativo
curl -X POST http://localhost:5000/api/jobs/{jobId}/match
```

Resposta inclui `overallScore` (0–100), sub-scores (técnico, domínio, senioridade,
localização, idioma), `recommendation`, `strengths`, `risks` e `rationale`.

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

- ✅ **Fase 1 — Core funcional** (esta entrega): solution, projetos, PostgreSQL + EF Core,
  entidades, migrations, CRUD de CandidateProfile e Company, listagem de JobPostings,
  Match Engine heurístico v1, análise manual via API, testes.
- ⏳ Fase 2 — Worker + jobs agendados + providers ATS (Greenhouse/Lever) + descoberta.
- ⏳ Fase 3 — LLM (análise + geração de mensagens), prompts versionados.
- ⏳ Fase 4 — Pipeline de oportunidades + recruiter leads.
- ⏳ Fase 5 — Digest por e-mail.

## Próximos passos sugeridos (Fase 2)

1. Criar `OpportunityOS.Worker` com Hangfire/Quartz.
2. `IJobSourceProvider` + `GreenhouseJobSourceProvider` e `LeverJobSourceProvider`.
3. `POST /api/jobs/discover` com deduplicação por `SourceProvider + ExternalId`
   (índice único já criado).
4. `ExecutionRun` para auditoria das execuções.
