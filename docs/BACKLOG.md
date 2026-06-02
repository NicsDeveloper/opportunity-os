# Opportunity OS — Backlog de desenvolvimento (Spec-Driven)

> Pendências após a evolução Firehose (P1–P16 + P0 + UI amigável já entregues).
> Cada item é uma mini-spec: **Objetivo · Escopo/Entregas · Critérios de aceite · DoD ·
> Dependências · Custo**. Ordenado por **prioridade** (B1 = faça primeiro).
> Padrão de DoD herdado da spec: entidades → migrations → endpoints → testes unitários →
> testes de integração nos fluxos principais → ExecutionRun → logs → README → frontend
> quando aplicável → o fluxo antigo continua funcionando.

Legenda de custo: 🟢 zero (sem APIs externas) · 🟡 LLM/Serper sob budget · 🔴 gasta cota real.

---

## B1 — Descoberta automática (agendar o Firehose no Worker) 🟢
**Por quê primeiro:** hoje a descoberta massiva só roda quando o usuário chama o endpoint.
Sem isso, o produto não é a "máquina que trabalha sozinha" — é uma ferramenta manual.

**Objetivo.** O sistema descobrir, promover e validar vagas continuamente, sozinho, dentro do budget.

**Escopo / Entregas.**
- `FirehoseSweepJob` (Hangfire) que roda uma busca ampla (`AggressiveSearchAsync`) numa cadência
  configurável (`Jobs:FirehoseCron`, ex.: `0 */4 * * *`), respeitando `QueryBudgetManager`.
- `PromotionJob` que roda `PromoteBatchAsync` (candidatos com empresa confirmada + confiança mínima).
- Agendamento opcional de `BacenFinancialSweep` e `ConsultingRadar` (crons próprios, desligados por
  flag por padrão por causa de custo).
- Registrar todos em `RecurringJobScheduler`; cada um com `ExecutionRun` (já nativo).

**Critérios de aceite.**
- Com o Worker rodando, novos `RawJobCandidate` e `JobPosting` aparecem sem ação manual.
- Nenhum job ultrapassa o budget diário (para com `CompletedWithBudgetLimit`).
- Crons configuráveis via `appsettings`; flags para ligar/desligar sweeps caros.

**DoD.** Jobs criados + agendados + flags + README; teste unitário do job (chama o serviço);
fluxo antigo intacto.
**Dependências.** P1, P9, P0 (prontos). **Custo.** 🟡 (Serper sob budget; controlável por cron/flag).

---

## B2 — Validação ao vivo em escala (1 varredura real com aval de cota) 🔴
**Objetivo.** Provar volume e qualidade reais das varreduras (P2/P3/P7) que só foram testadas
unitariamente + smoke de 1 crédito.

**Escopo / Entregas.**
- Rodar `aggressive-search` (200 queries), `bacen-financial-sweep` (top instituições) e
  `consulting-radar/discover` uma vez, com budget definido.
- Medir via `/api/discovery/metrics` e `/provider-quality`; ajustar gates/blocklist se preciso.
- Relatório curto: nº de candidatos, % por fonte, promovidos, custo em créditos.

**Critérios de aceite.** Centenas de candidatos coletados; agregadores marcados; empresas reais
resolvidas; nenhuma vaga "lixo" no Action Today; custo dentro do orçado.
**DoD.** Execução registrada (ExecutionRun) + ajustes commitados + nota no README.
**Dependências.** B1 ideal (mas roda manual). **Custo.** 🔴 (dezenas–centenas de créditos Serper).

---

## B3 — Telas para Bancos/Bacen e Consultorias (+ campanhas) 🟢
**Objetivo.** Expor no frontend o que hoje só existe via API, em linguagem de usuário.

**Escopo / Entregas.**
- Tela "Empresas-alvo": prévia do Bacen sweep (quantas por prioridade, estimativa) + botão "Procurar
  vagas nessas empresas".
- Tela "Consultorias": lista de candidatos (com sinais explicáveis em PT), botão "Salvar empresa"
  (promover) com o gate de confiança.
- Tela simples de "Campanhas" (listar/ativar/rodar) — sem jargão.

**Critérios de aceite.** Usuário roda Bacen sweep e promove consultorias sem tocar em API;
textos sem jargão; ações com feedback (toast).
**DoD.** Componentes React + `api.ts` + estados de loading/erro; build limpo; validado na preview.
**Dependências.** P2, P3, P8. **Custo.** 🟢 (telas; a busca em si é 🟡).

---

## B4 — Ações que faltam no "Explorar tudo" 🟢
**Objetivo.** Completar as ações da spec P8 no card de vaga bruta.

**Escopo / Entregas.**
- Botão "Achar vaga original" → `POST /raw-candidates/{id}/resolve-original` (endpoint já existe);
  mostra o resultado (status de verificação) no card.
- "Marcar duplicada" e "Empresa errada" já existem como feedback; adicionar "Ocultar"
  (esconde localmente / feedback `HideSimilar`).
- No "Boas opções": "Ocultar similares" (por fingerprint).

**Critérios de aceite.** Cada ação dá retorno visível; "achar original" atualiza o selo de fonte;
nada quebra o fluxo de salvar/feedback.
**DoD.** Frontend + `api.ts`; build limpo; validado na preview.
**Dependências.** P7, P11. **Custo.** 🟡 ("achar original" usa 1 busca).

---

## B5 — Testes de integração dos fluxos principais 🟢
**Objetivo.** Cobrir o que os unitários não cobrem: endpoints reais ponta a ponta.

**Escopo / Entregas.**
- `WebApplicationFactory` (já há `public partial class Program`) + Postgres de teste
  (Testcontainers ou banco dedicado).
- Fluxos: quick-search salva candidato; promote cria JobPosting + ocorrência; `/api/matches?sort=rank`;
  `/api/feedback`; `/api/discovery/metrics`; bacen-sweep/preview.

**Critérios de aceite.** Suite de integração verde no CI local; cobre os caminhos felizes + 1 erro
por endpoint crítico.
**DoD.** Projeto/aquivos de teste de integração + README "como rodar"; não depende de Serper/LLM
(usa fakes/registros existentes).
**Dependências.** P1, P0, P15, P16, P11, P2. **Custo.** 🟢.

---

## B6 — Fila de análise LLM (LlmAnalysisQueue) 🟡
**Objetivo.** Formalizar a priorização da IA que hoje é só por gates.

**Escopo / Entregas.**
- Entidade/serviço `LlmAnalysisQueue` com prioridades: 1) user-requested, 2) Action, 3) Qualified,
  4) Firehose. Worker consome respeitando `LlmDailyAutoAnalyses`.
- Enfileirar em vez de analisar inline na descoberta; processar fora do caminho crítico.

**Critérios de aceite.** Itens de maior prioridade são analisados primeiro; budget respeitado;
nada de LLM em lixo do Firehose.
**DoD.** Entidade + migration + worker + testes; README.
**Dependências.** P10, P9. **Custo.** 🟡. **Nota:** valor incremental sobre o que já existe (gates).

---

## B7 — Feedback influencia o ranking 🟢
**Objetivo.** Fechar o loop de aprendizado: 👍/👎 mudam a ordem.

**Escopo / Entregas.**
- `UserFeedbackBoost` no `DiscoveryRank` passa a ser calculado a partir do `UserFeedback`
  (por empresa/fonte/vaga): relevante sobe, irrelevante/HideSimilar desce; "empresa errada" reduz
  confiança da fonte.
- Opcional: penalizar fontes com muito feedback negativo.

**Critérios de aceite.** Marcar irrelevante reduz a posição da vaga/itens similares na próxima carga.
**DoD.** Cálculo + testes unitários do boost; README.
**Dependências.** P11, P15. **Custo.** 🟢.

---

## B8 — Fallback de IA no resolver de empresa (P5) 🟡
**Objetivo.** Resolver empresa quando a heurística falhar, sob budget.

**Escopo / Entregas.**
- Em `CompanyNameResolver`, quando heurística retorna null e o candidato tem potencial
  (fit/fonte ok), chamar LLM (gate de budget) pra extrair a empresa do título/snippet.

**Critérios de aceite.** Mais candidatos com empresa confirmada sem aumentar falsos positivos;
LLM só quando heurística falha e dentro do budget.
**DoD.** Implementação + teste com fake LLM; README.
**Dependências.** P5, P9, P10. **Custo.** 🟡.

---

## B9 — Providers dedicados por plataforma (P13 "de verdade") 🟡
**Objetivo.** Parsers próprios para as plataformas que o Provider Quality Dashboard mostrar valerem a pena.

**Escopo / Entregas.** Um `IJobSourceProvider` por plataforma priorizada (ex.: Workday, Teamtailor,
Recruitee, Workable, Breezy, Programathor, GeekHunter, Coodesh, Remotar, APInfo, Trampos), **um de cada vez**.
Cada um: SourceConfidence, dedup, rate-limit, **fixture**, tratamento de erro, métricas, `ExecutionRun`.

**Critérios de aceite (por provider).** Fixture-based unit test; aparece no provider-quality;
não duplica; respeita rate-limit.
**DoD.** Por provider: provider + fixture + teste + DI/flag + README.
**Dependências.** P4, P6, P12 (e idealmente B2 pra saber quais priorizar). **Custo.** 🟡 por provider.

---

## B10 — Housekeeping 🟢
**Objetivo.** Acabamento e dívidas pequenas.

**Escopo / Entregas.**
- Abrir o PR (`gh` ou link de compare) — branch já pushada.
- Resolver/avaliar os 18 *warnings* (em especial `System.Security.Cryptography.Xml` / NU1903).
- Backfill de `SourceType`/`SourceConfidence` nas vagas antigas (hoje "Fonte a confirmar").
- Melhorar nome de empresa derivado do host (ex.: "Br") — heurística ou descarte.
- (Opcional) renomear o produto no topo ("Opportunity OS" → algo pessoal).

**Critérios de aceite.** PR aberto; sem warning de vulnerabilidade; vagas antigas com fonte coerente.
**DoD.** Itens commitados; README/changelog.
**Dependências.** — **Custo.** 🟢.

---

## Resumo da ordem recomendada
1. **B1** Agendamento automático (vira "máquina") 🟡
2. **B2** Varredura real 1× (prova o volume) 🔴
3. **B3** Telas Bacen/Consultorias/Campanhas 🟢
4. **B4** Ações que faltam no Explorar tudo 🟡
5. **B5** Testes de integração 🟢
6. **B6** Fila de LLM 🟡
7. **B7** Feedback no ranking 🟢
8. **B8** Fallback IA no resolver de empresa 🟡
9. **B9** Providers dedicados (incremental) 🟡
10. **B10** Housekeeping (PR, warnings, backfill) 🟢

**Regra de custo:** B1/B2/B4/B6/B8/B9 tocam Serper/LLM — sempre sob `QueryBudgetManager`.
Os 🟢 (B3/B5/B7/B10) podem ser feitos a qualquer momento sem gastar cota.
