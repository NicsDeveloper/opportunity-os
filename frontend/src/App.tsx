import { useEffect, useState } from "react";
import { Icon } from "./icons";
import {
  api, type BestOpportunity, type GeneratedMessage, type Run,
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

function Feed({ reload, notify, onChanged }: {
  reload: number; notify: (m: string) => void; onChanged: () => void;
}) {
  const summary = useAsync(api.summary, [reload]);
  const opps = useAsync(() => api.bestOpportunities(40), [reload]);
  const [busy, setBusy] = useState(false);
  const s = summary.data;

  const runSearch = async () => {
    setBusy(true); notify("Buscando vagas .NET…");
    try {
      await api.search([".net", "c#", "desenvolvedor .net", "backend .net", "pagamentos"]);
      notify("Busca disparada. Os resultados aparecem aqui em instantes.");
      onChanged();
    } catch { notify("Falha na busca."); }
    finally { setBusy(false); }
  };

  return (
    <>
      <div className="statgrid">
        <Stat icon="target" label="Oportunidades relevantes" n={opps.data?.length}
          delta={s ? `${s.matchesAbove75} fortes (75+)` : ""} />
        <Stat icon="briefcase" label="Vagas descobertas" n={s?.jobsDiscovered}
          delta={s ? `+${s.jobsToday} hoje` : ""} />
        <Stat icon="chat" label="Mensagens geradas" n={s?.messagesGenerated}
          delta={s ? `+${s.messagesToday} hoje` : ""} />
        <Stat icon="calendar" label="Follow-ups pendentes" n={s?.followUpsPending}
          delta={s?.nextFollowUpInDays != null ? `Próximo: ${s.nextFollowUpInDays} dia(s)` : "—"} mutedDelta />
      </div>

      <div className="panel">
        <div className="panel-head">
          <div>
            <h3>Melhores oportunidades</h3>
            <p className="sub" style={{ margin: "2px 0 0" }}>
              Ordenadas por relevância para o seu perfil .NET. Clique em “Gerar mensagem” para um rascunho pronto pra copiar.
            </p>
          </div>
          <button className="btn primary" disabled={busy} onClick={runSearch}>
            <Icon name="search" /> {busy ? "Buscando…" : "Buscar agora"}
          </button>
        </div>

        {opps.error && <p className="err">{opps.error}</p>}
        {(opps.data ?? []).map((o) => (
          <OppCard key={o.matchId} o={o} notify={notify} onChanged={onChanged} />
        ))}
        {opps.data?.length === 0 && (
          <p className="placeholder">
            Nenhuma oportunidade relevante ainda. Clique em <strong>Buscar agora</strong> — a descoberta contínua
            também roda sozinha em segundo plano.
          </p>
        )}
      </div>
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
function OppCard({ o, notify, onChanged }: {
  o: BestOpportunity; notify: (m: string) => void; onChanged: () => void;
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
            {why && <div className="why"><strong>Por que combina:</strong> {why}</div>}
            {(isTerse && !analyzed) && (
              <button className="why-link" disabled={analyzing} onClick={analyze}>
                {analyzing ? "Analisando…" : "↻ Analisar com IA (por que combina em detalhe)"}
              </button>
            )}
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
