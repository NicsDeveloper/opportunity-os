import { useEffect, useState } from "react";
import { Icon } from "./icons";
import {
  api, type BestOpportunity, type GeneratedMessage, type RawCandidate, type Run,
} from "./api";

export function App() {
  const [reload, setReload] = useState(0);
  const [toast, setToast] = useState<string | null>(null);
  const profile = useAsync(api.profile, []);
  const firstName = (profile.data?.fullName ?? "").trim().split(" ")[0] || "candidato";

  const notify = (m: string) => { setToast(m); setTimeout(() => setToast(null), 3500); };
  const refresh = () => setReload((r) => r + 1);

  // Keep the screen fresh while the system discovers in the background ("ao vivo").
  useEffect(() => {
    const id = setInterval(() => setReload((r) => r + 1), 20000);
    return () => clearInterval(id);
  }, []);

  return (
    <div className="layout">
      <StatusRail reload={reload} name={profile.data?.fullName} headline={profile.data?.headline} />
      <main className="main">
        <div className="main-inner">
          <Topbar firstName={firstName} />
          <Feed reload={reload} notify={notify} onChanged={refresh} />
        </div>
      </main>
      {toast && <div className="toast">{toast}</div>}
    </div>
  );
}

/* ---------- left status rail (system "ao vivo") ---------- */

function StatusRail({ reload, name, headline }: { reload: number; name?: string; headline?: string }) {
  const runs = useAsync(() => api.runs(6), [reload]);
  const last = runs.data?.[0];
  return (
    <aside className="sidebar">
      <div className="brand"><span className="mark">◎</span> Opportunity OS</div>

      <div className="live">
        <span className="pulse" />
        <div>
          <div className="live-t">Descoberta contínua</div>
          <div className="live-s">
            {last ? `Última atividade ${ago(last.startedAtUtc)}` : "Aguardando primeira execução…"}
          </div>
        </div>
      </div>

      <div className="nav-label">Atividade do sistema</div>
      <div className="rail-runs">
        {(runs.data ?? []).map((r) => <RunRow key={r.id} r={r} />)}
        {runs.data?.length === 0 && <p className="placeholder small">Nenhuma execução ainda.</p>}
      </div>

      <div className="usercard">
        <span className="avatar">{initials(name ?? "NS")}</span>
        <div><div className="nm">{name ?? "—"}</div><div className="rl">{headline ?? ""}</div></div>
      </div>
    </aside>
  );
}

function Topbar({ firstName }: { firstName: string }) {
  return (
    <div className="topbar">
      <div className="greeting">
        <h2>{greeting()}, {firstName}! 👋</h2>
        <p>Suas oportunidades .NET mais relevantes e recentes, atualizadas ao vivo.</p>
      </div>
      <div className="topbar-right">
        <span className="avatar">{initials(firstName)}</span>
      </div>
    </div>
  );
}

/* ---------- the one living screen ---------- */

const PAGE_SIZE = 6;
const SEARCH_KEYWORDS = [
  "desenvolvedor .net", "desenvolvedor backend c#", "engenheiro de software .net",
  "programador c# pleno", "vaga .net remoto", "desenvolvedor .net fintech",
];

type FeedView = "action" | "qualified" | "firehose";

function Feed({ reload, notify, onChanged }: {
  reload: number; notify: (m: string) => void; onChanged: () => void;
}) {
  const summary = useAsync(api.summary, [reload]);
  const [view, setView] = useState<FeedView>("action");
  const [busy, setBusy] = useState(false);
  const s = summary.data;

  const runSearch = async () => {
    setBusy(true); notify("Buscando vagas .NET…");
    try {
      await api.search(SEARCH_KEYWORDS);
      notify("Busca disparada. Os resultados aparecem aqui em instantes.");
      onChanged();
    } catch { notify("Falha na busca."); }
    finally { setBusy(false); }
  };

  return (
    <>
      <div className="statgrid">
        <Stat icon="target" label="Acionáveis hoje" n={s?.matchesAbove75}
          delta={s ? `+${s.matchesAbove75Today} hoje` : ""} />
        <Stat icon="briefcase" label="Vagas descobertas" n={s?.jobsDiscovered}
          delta={s ? `+${s.jobsToday} hoje` : ""} />
        <Stat icon="chat" label="Mensagens geradas" n={s?.messagesGenerated}
          delta={s ? `+${s.messagesToday} hoje` : ""} />
        <Stat icon="calendar" label="Follow-ups pendentes" n={s?.followUpsPending}
          delta={s?.nextFollowUpInDays != null ? `Próximo: ${s.nextFollowUpInDays} dia(s)` : "—"} mutedDelta />
      </div>

      <div className="panel">
        <div className="panel-head">
          <div className="tabs">
            <button className={"tab" + (view === "action" ? " active" : "")} onClick={() => setView("action")}>
              ⚡ Action Today
            </button>
            <button className={"tab" + (view === "qualified" ? " active" : "")} onClick={() => setView("qualified")}>
              ✓ Qualified
            </button>
            <button className={"tab" + (view === "firehose" ? " active" : "")} onClick={() => setView("firehose")}>
              🌊 Firehose
            </button>
          </div>
          <button className="btn primary" disabled={busy} onClick={runSearch}>
            <Icon name="search" /> {busy ? "Buscando…" : "Buscar agora"}
          </button>
        </div>

        {view === "action" && <ActionView reload={reload} notify={notify} onChanged={onChanged} />}
        {view === "qualified" && <QualifiedView reload={reload} notify={notify} onChanged={onChanged} />}
        {view === "firehose" && <FirehoseView reload={reload} notify={notify} onChanged={onChanged} />}
      </div>
    </>
  );
}

/* ---------- Action Today: poucas, acionáveis (Fit >= 75) ---------- */
function ActionView({ reload, notify, onChanged }: { reload: number; notify: (m: string) => void; onChanged: () => void }) {
  const opps = useAsync(api.actionToday, [reload]);
  const all = opps.data ?? [];
  return (
    <>
      <p className="sub" style={{ marginTop: 0 }}>Quais vagas atacar hoje: relevância alta, frescas, fonte verificável.</p>
      {opps.error && <p className="err">{opps.error}</p>}
      {all.map((o) => <OppCard key={o.matchId} o={o} notify={notify} onChanged={onChanged} mode="action" />)}
      {all.length === 0 && <p className="placeholder">Nada acionável agora. Veja <strong>Qualified</strong> ou rode uma busca.</p>}
    </>
  );
}

/* ---------- Qualified: triado, ordenado por DiscoveryRank, paginado ---------- */
function QualifiedView({ reload, notify, onChanged }: { reload: number; notify: (m: string) => void; onChanged: () => void }) {
  const opps = useAsync(() => api.qualified(120), [reload]);
  const [page, setPage] = useState(0);
  const all = opps.data ?? [];
  const pageCount = Math.max(1, Math.ceil(all.length / PAGE_SIZE));
  const current = Math.min(page, pageCount - 1);
  useEffect(() => { setPage(0); }, [opps.data?.length]);
  return (
    <>
      <p className="sub" style={{ marginTop: 0 }}>O que parece bom após triagem — ordenado por DiscoveryRank (relevância + fonte + frescor).</p>
      {opps.error && <p className="err">{opps.error}</p>}
      {all.slice(current * PAGE_SIZE, current * PAGE_SIZE + PAGE_SIZE).map((o) =>
        <OppCard key={o.matchId} o={o} notify={notify} onChanged={onChanged} mode="qualified" />)}
      {all.length === 0 && <p className="placeholder">Sem vagas qualificadas ainda.</p>}
      {all.length > PAGE_SIZE && (
        <div className="pager">
          <button className="btn" disabled={current === 0} onClick={() => setPage(current - 1)}>‹ Anterior</button>
          <span className="pager-info">Página {current + 1} de {pageCount} · {all.length} vagas</span>
          <button className="btn" disabled={current >= pageCount - 1} onClick={() => setPage(current + 1)}>Próxima ›</button>
        </div>
      )}
    </>
  );
}

/* ---------- Firehose: tudo que foi encontrado (RawJobCandidate) ---------- */
function FirehoseView({ reload, notify, onChanged }: { reload: number; notify: (m: string) => void; onChanged: () => void }) {
  const raw = useAsync(() => api.rawCandidates(200), [reload]);
  const metrics = useAsync(api.discoveryMetrics, [reload]);
  const [page, setPage] = useState(0);
  const all = raw.data ?? [];
  const FIRE_PAGE = 12;
  const pageCount = Math.max(1, Math.ceil(all.length / FIRE_PAGE));
  const current = Math.min(page, pageCount - 1);
  const m = metrics.data;
  useEffect(() => { setPage(0); }, [raw.data?.length]);
  return (
    <>
      <p className="sub" style={{ marginTop: 0 }}>Tudo que o sistema encontrou — com ruído. Classificado por fonte; promova o que valer.</p>
      {m && (
        <div className="fire-metrics">
          <span><b>{m.rawCandidatesThisWeek}</b> brutos/semana</span>
          <span><b>{m.queriesToday}</b> queries hoje</span>
          <span><b>{m.jobsPromotedToday}</b> promovidas hoje</span>
          <span><b>{m.weakSources}</b> fontes fracas</span>
          <span><b>{(m.deduplicationRate * 100).toFixed(0)}%</b> duplicadas</span>
        </div>
      )}
      {raw.error && <p className="err">{raw.error}</p>}
      {all.slice(current * FIRE_PAGE, current * FIRE_PAGE + FIRE_PAGE).map((c) =>
        <RawCandidateCard key={c.id} c={c} notify={notify} onChanged={onChanged} />)}
      {all.length === 0 && <p className="placeholder">Firehose vazio. Rode <strong>Buscar agora</strong> ou uma campanha agressiva.</p>}
      {all.length > FIRE_PAGE && (
        <div className="pager">
          <button className="btn" disabled={current === 0} onClick={() => setPage(current - 1)}>‹ Anterior</button>
          <span className="pager-info">Página {current + 1} de {pageCount} · {all.length} candidatos</span>
          <button className="btn" disabled={current >= pageCount - 1} onClick={() => setPage(current + 1)}>Próxima ›</button>
        </div>
      )}
    </>
  );
}

function Stat({ icon, label, n, delta, mutedDelta }: {
  icon: string; label: string; n?: number; delta: string; mutedDelta?: boolean;
}) {
  return (
    <div className="stat">
      <div className="ic"><Icon name={icon} /></div>
      <div className="l">{label}</div>
      <div className="n">{n ?? "…"}</div>
      <div className={"d" + (mutedDelta ? " muted" : "")}>{delta}</div>
    </div>
  );
}

function Logo({ name, website }: { name: string; website?: string | null }) {
  const [i, setI] = useState(0);
  const host = hostOf(website);
  const sources = host
    ? [`https://logo.clearbit.com/${host}`, `https://www.google.com/s2/favicons?sz=64&domain=${host}`]
    : [];
  if (i < sources.length)
    return <img className="logo img" alt={name} src={sources[i]} onError={() => setI(i + 1)} />;
  return <span className="logo" style={{ background: logoColor(name) }}>{initials(name)}</span>;
}

/* one opportunity, with inline expandable generated message */
function OppCard({ o, notify, onChanged, mode }: {
  o: BestOpportunity; notify: (m: string) => void; onChanged: () => void; mode?: "action" | "qualified";
}) {
  const [msg, setMsg] = useState<GeneratedMessage | null>(null);
  const [busy, setBusy] = useState(false);
  const [open, setOpen] = useState(false);
  const [why, setWhy] = useState(o.rationale);
  const [score, setScore] = useState(o.overallScore);
  const [rec, setRec] = useState(o.recommendation);
  const [analyzing, setAnalyzing] = useState(false);
  const [analyzed, setAnalyzed] = useState(false);

  // Heuristic rationale reads like "Score 68/100 — Técnico 65…"; the LLM one is prose.
  const isTerse = /^score\s+\d+\/100/i.test(why.trim());

  const generate = async () => {
    if (msg) { setOpen((v) => !v); return; }
    setBusy(true); notify("Gerando mensagem…");
    try {
      const m = await api.generateOutreach(o.jobPostingId);
      setMsg(m); setOpen(true); notify("Mensagem gerada — revise e copie.");
      onChanged();
    } catch { notify("Não foi possível gerar (score abaixo do mínimo?)."); }
    finally { setBusy(false); }
  };

  const analyze = async () => {
    setAnalyzing(true); notify("Analisando com IA…");
    try {
      const r = await api.analyze(o.jobPostingId);
      setWhy(r.match.rationale); setScore(r.match.overallScore); setRec(r.match.recommendation);
      setAnalyzed(true); notify("Análise concluída.");
      onChanged();
    } catch { notify("Não foi possível analisar agora."); }
    finally { setAnalyzing(false); }
  };

  return (
    <div className="oppcard">
      <div className="opp">
        <div className="who">
          <Logo name={o.companyName} website={o.companyWebsiteUrl} />
          <div>
            <div className="title">{o.jobTitle}</div>
            <div className="chips">{o.skills.slice(0, 6).map((sk) => <span className="chip" key={sk}>{sk}</span>)}</div>
            <SourceBadge sourceType={o.sourceType} confidence={o.sourceConfidenceScore}
              sourceName={o.sourceName} realCompany={o.realCompanyName} manual={o.requiresManualValidation}
              rank={mode === "qualified" ? o.discoveryRank : undefined} />
            {why && <div className="why"><strong>Por que combina:</strong> {why}</div>}
            {(isTerse && !analyzed) && (
              <button className="why-link" disabled={analyzing} onClick={analyze}>
                {analyzing ? "Analisando…" : "↻ Analisar com IA (por que combina em detalhe)"}
              </button>
            )}
            <FeedbackBar notify={notify} body={{ jobPostingId: o.jobPostingId }} extra={mode === "action"} />
          </div>
        </div>
        <div className="company">
          {o.companyName}
          <div className={"posted" + (isStale(o.postedAtUtc) ? " stale" : "")}>{ago(o.postedAtUtc)}</div>
        </div>
        <div className="ring" style={{ borderColor: ringColor(score) }}>{score}</div>
        <div className={"rec " + recClass(rec)}>{recLabel(rec)}</div>
        <div className="opp-actions">
          <a className="btn" href={o.jobUrl} target="_blank" rel="noreferrer">Ver vaga</a>
          <button className="btn primary" disabled={busy} onClick={generate}>
            {busy ? "Gerando…" : msg ? (open ? "Ocultar mensagem" : "Ver mensagem") : "Gerar mensagem"}
          </button>
        </div>
      </div>

      {msg && open && <MessagePanel msg={msg} notify={notify} />}
    </div>
  );
}

/* source provenance badge: "Empresa X via Fonte Y" + tipo/confiança */
function SourceBadge({ sourceType, confidence, sourceName, realCompany, manual, rank }: {
  sourceType?: string; confidence?: number; sourceName?: string | null;
  realCompany?: string | null; manual?: boolean; rank?: number;
}) {
  if (!sourceType && !sourceName && rank == null) return null;
  const cls = sourceType === "OfficialAts" || sourceType === "OfficialCareerPage" ? "src-official"
    : sourceType === "SocialIndexed" ? "src-social"
    : sourceType === "Aggregator" ? "src-agg" : "src-web";
  return (
    <div className="srcbadge">
      {sourceType && <span className={"src-pill " + cls}>{sourceTypeLabel(sourceType)} · {confidence ?? 0}</span>}
      {realCompany
        ? <span className="src-co">{realCompany}{sourceName ? <> via {sourceName}</> : null}</span>
        : (sourceName && <span className="src-co muted">empresa não confirmada · via {sourceName}</span>)}
      {manual && <span className="src-pill src-manual">⚠ revisão manual</span>}
      {rank != null && <span className="src-pill src-rank">rank {rank}</span>}
    </div>
  );
}

/* quick feedback buttons (P11) */
function FeedbackBar({ notify, body, extra }: {
  notify: (m: string) => void; body: { jobPostingId?: string; rawJobCandidateId?: string }; extra?: boolean;
}) {
  const [sent, setSent] = useState<string | null>(null);
  const send = async (type: string, label: string) => {
    try { await api.feedback(type, body); setSent(label); notify(`Feedback: ${label}`); }
    catch { notify("Não foi possível registrar o feedback."); }
  };
  if (sent) return <div className="fbbar"><span className="fb-done">✓ {sent}</span></div>;
  return (
    <div className="fbbar">
      <button className="fb" onClick={() => send("Relevant", "relevante")}>👍 Relevante</button>
      <button className="fb" onClick={() => send("Irrelevant", "irrelevante")}>👎 Irrelevante</button>
      <button className="fb" onClick={() => send("BadCompanyDetection", "empresa errada")}>🏢 Empresa errada</button>
      {extra && <button className="fb" onClick={() => send("Applied", "já apliquei")}>✅ Já apliquei</button>}
    </div>
  );
}

/* one raw firehose candidate */
function RawCandidateCard({ c, notify, onChanged }: {
  c: RawCandidate; notify: (m: string) => void; onChanged: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [promoted, setPromoted] = useState<string | null>(null);
  const promote = async () => {
    setBusy(true); notify("Promovendo…");
    try {
      const r = await api.promoteRaw(c.id);
      setPromoted(r.promoted ? (r.wasDuplicate ? "duplicada (ocorrência)" : "promovida") : null);
      notify(r.reason);
      if (r.promoted) onChanged();
    } catch { notify("Falha ao promover."); }
    finally { setBusy(false); }
  };
  return (
    <div className="rawcard">
      <div className="raw-main">
        <div className="title">{c.title}</div>
        <SourceBadge sourceType={c.sourceType} confidence={c.sourceConfidenceScore}
          sourceName={c.sourceName} realCompany={c.realCompanyName} manual={c.requiresManualValidation} />
        {c.snippet && <div className="raw-snippet">{c.snippet}</div>}
        <FeedbackBar notify={notify} body={{ rawJobCandidateId: c.id }} />
      </div>
      <div className="raw-meta">
        <span className="posted">{ago(c.discoveredAtUtc)}</span>
        <span className={"pill raw-status"}>{c.status}</span>
      </div>
      <div className="opp-actions">
        <a className="btn" href={c.discoveredUrl} target="_blank" rel="noreferrer">Abrir</a>
        {promoted
          ? <span className="fb-done">✓ {promoted}</span>
          : <button className="btn primary" disabled={busy || !c.realCompanyName} title={c.realCompanyName ? "" : "empresa não confirmada"} onClick={promote}>
              {busy ? "…" : "Promover"}
            </button>}
      </div>
    </div>
  );
}

function MessagePanel({ msg, notify }: { msg: GeneratedMessage; notify: (m: string) => void }) {
  return (
    <div className="msgpanel">
      <p className="msg-note"><Icon name="user" /> Rascunho para revisão humana — nada é enviado automaticamente.</p>
      <CopyBlock label="Mensagem (LinkedIn / direta)" text={msg.linkedInMessage} notify={notify} />
      <CopyBlock label="Assunto do e-mail" text={msg.emailSubject} notify={notify} single />
      <CopyBlock label="Corpo do e-mail" text={msg.emailBody} notify={notify} />
      {msg.followUpMessage && <CopyBlock label="Follow-up (depois)" text={msg.followUpMessage} notify={notify} />}
      {msg.humanReviewNotes && (
        <div className="review"><strong>Observações para revisão:</strong> {msg.humanReviewNotes}</div>
      )}
    </div>
  );
}

function CopyBlock({ label, text, notify, single }: {
  label: string; text: string; notify: (m: string) => void; single?: boolean;
}) {
  const copy = async () => {
    try { await navigator.clipboard.writeText(text); notify(`Copiado: ${label}`); }
    catch { notify("Não foi possível copiar."); }
  };
  return (
    <div className="copyblock">
      <div className="copyblock-head">
        <span className="cb-label">{label}</span>
        <button className="btn copy" onClick={copy}>Copiar</button>
      </div>
      {single
        ? <div className="cb-single">{text}</div>
        : <pre className="cb-text">{text}</pre>}
    </div>
  );
}

function RunRow({ r }: { r: Run }) {
  const cls = r.status === "Succeeded" ? "ok" : r.status === "Failed" ? "fail" : r.status === "PartiallyFailed" ? "part" : "run";
  return (
    <div className="run">
      <span className={"dot " + cls} />
      <div><div className="t">{runLabel(r.runType)}</div><div className="s">{new Date(r.startedAtUtc).toLocaleString()}</div></div>
      <div className="meta">{r.itemsProcessed}·{r.itemsSucceeded}ok</div>
    </div>
  );
}

/* ---------- helpers ---------- */

function useAsync<T>(fn: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let active = true; setError(null);
    fn().then((d) => active && setData(d)).catch((e) => active && setError(String(e)));
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);
  return { data, error };
}

function hostOf(url?: string | null) {
  if (!url) return null;
  try { return new URL(url).host; } catch { return null; }
}
function greeting() { const h = new Date().getHours(); return h < 12 ? "Bom dia" : h < 18 ? "Boa tarde" : "Boa noite"; }
function initials(name: string) {
  const p = name.trim().split(/\s+/);
  return ((p[0]?.[0] ?? "") + (p[1]?.[0] ?? "")).toUpperCase() || "?";
}
function logoColor(name: string) {
  let h = 0; for (const c of name) h = (h * 31 + c.charCodeAt(0)) % 360;
  return `hsl(${h} 55% 45%)`;
}
function ringColor(score: number) { return score >= 75 ? "#16a34a" : score >= 60 ? "#2563eb" : "#9ca3af"; }
function recLabel(r: string) {
  if (r === "Strategic" || r === "Prioritize") return "Excelente match";
  if (r === "Apply") return "Bom match";
  if (r === "SaveForLater") return "Vale revisar";
  return "Baixo";
}
function sourceTypeLabel(t: string) {
  const map: Record<string, string> = {
    OfficialAts: "ATS oficial", OfficialCareerPage: "Carreira oficial", JobBoard: "Job board",
    Aggregator: "Agregador", SearchResult: "Web", SocialIndexed: "LinkedIn/social", Unknown: "?",
  };
  return map[t] ?? t;
}
function recClass(r: string) {
  if (r === "Strategic" || r === "Prioritize") return "exc";
  if (r === "Apply") return "bom";
  return "low";
}
function runLabel(t: string) {
  const map: Record<string, string> = {
    DiscoverJobs: "Busca de vagas (ATS)", SearchJobs: "Busca por palavras-chave",
    SendDailyDigest: "Envio de digest", CompanyOnboarding: "Onboarding de empresas",
    BacenPixParticipantsImport: "Import Bacen", BacenPixParticipantsPromotion: "Promoção Bacen",
  };
  return map[t] ?? t;
}
function ago(iso: string) {
  const days = Math.floor((Date.now() - +new Date(iso)) / 86400000);
  if (days <= 0) {
    const h = Math.floor((Date.now() - +new Date(iso)) / 3600000);
    if (h <= 0) { const m = Math.floor((Date.now() - +new Date(iso)) / 60000); return m <= 1 ? "agora há pouco" : `há ${m} min`; }
    return `há ${h} h`;
  }
  if (days < 30) return `há ${days} dia(s)`;
  if (days < 365) return `há ${Math.floor(days / 30)} mes(es)`;
  return `há ${Math.floor(days / 365)} ano(s)`;
}
function isStale(iso: string) { return (Date.now() - +new Date(iso)) / 86400000 > 60; }
