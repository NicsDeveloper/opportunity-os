import { useEffect, useState } from "react";
import { Icon } from "./icons";
import {
  api, type BestOpportunity, type Company, type Job, type Message,
  type Opportunity, type Run,
} from "./api";

type View =
  | "dashboard" | "jobs" | "companies" | "matches" | "messages" | "followups"
  | "search" | "runs" | "insights" | "reports" | "profile" | "prefs";

const NAV: { label: string; items: [View, string, string][] }[] = [
  { label: "", items: [["dashboard", "Dashboard", "dashboard"]] },
  { label: "Oportunidades", items: [
    ["jobs", "Vagas", "briefcase"], ["companies", "Empresas", "building"],
    ["matches", "Matches", "target"], ["messages", "Mensagens", "chat"],
    ["followups", "Follow-ups", "bell"],
  ]},
  { label: "Busca", items: [["search", "Busca ativa", "search"], ["runs", "Execuções", "bolt"]] },
  { label: "Análises", items: [["insights", "Insights", "chart"], ["reports", "Relatórios", "doc"]] },
  { label: "Configurações", items: [["profile", "Perfil", "user"], ["prefs", "Preferências", "gear"]] },
];

export function App() {
  const [view, setView] = useState<View>("dashboard");
  const [reload, setReload] = useState(0);
  const [toast, setToast] = useState<string | null>(null);
  const profile = useAsync(api.profile, []);
  const firstName = (profile.data?.fullName ?? "").trim().split(" ")[0] || "candidato";

  const notify = (m: string) => { setToast(m); setTimeout(() => setToast(null), 3500); };
  const refresh = () => setReload((r) => r + 1);

  return (
    <div className="layout">
      <Sidebar view={view} onNav={setView} name={profile.data?.fullName} headline={profile.data?.headline} />
      <main className="main">
        <Topbar firstName={firstName} />
        {view === "dashboard" && <Dashboard reload={reload} onNav={setView} notify={notify} onChanged={refresh} />}
        {view === "jobs" && <JobsPage reload={reload} />}
        {view === "companies" && <CompaniesPage reload={reload} />}
        {view === "matches" && <MatchesPage reload={reload} />}
        {view === "messages" && <MessagesPage reload={reload} />}
        {view === "followups" && <FollowUpsPage reload={reload} />}
        {view === "search" && <SearchPage notify={notify} onChanged={refresh} />}
        {view === "runs" && <RunsPage reload={reload} />}
        {view === "insights" && <Placeholder title="Insights" />}
        {view === "reports" && <Placeholder title="Relatórios" />}
        {view === "profile" && <ProfilePage />}
        {view === "prefs" && <Placeholder title="Preferências" />}
      </main>
      {toast && <div className="toast">{toast}</div>}
    </div>
  );
}

/* ---------- layout ---------- */

function Sidebar({ view, onNav, name, headline }: {
  view: View; onNav: (v: View) => void; name?: string; headline?: string;
}) {
  return (
    <aside className="sidebar">
      <div className="brand"><span className="mark">◎</span> Opportunity OS</div>
      {NAV.map((group, i) => (
        <div key={i}>
          {group.label && <div className="nav-label">{group.label}</div>}
          {group.items.map(([id, label, icon]) => (
            <button key={id} className={"nav-item" + (view === id ? " active" : "")} onClick={() => onNav(id)}>
              <Icon name={icon} /> {label}
            </button>
          ))}
        </div>
      ))}
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
        <p>Aqui está o resumo das suas oportunidades.</p>
      </div>
      <div className="topbar-right">
        <span className="bell"><Icon name="bell" /></span>
        <span className="avatar">{initials(firstName)}</span>
      </div>
    </div>
  );
}

/* ---------- dashboard ---------- */

function Dashboard({ reload, onNav, notify, onChanged }: {
  reload: number; onNav: (v: View) => void; notify: (m: string) => void; onChanged: () => void;
}) {
  const summary = useAsync(api.summary, [reload]);
  const opps = useAsync(() => api.bestOpportunities(5), [reload]);
  const runs = useAsync(() => api.runs(4), [reload]);
  const followUps = useAsync(api.opportunities, [reload]);

  const s = summary.data;
  const upcoming = (followUps.data ?? [])
    .filter((o) => o.nextFollowUpAtUtc)
    .sort((a, b) => +new Date(a.nextFollowUpAtUtc!) - +new Date(b.nextFollowUpAtUtc!))
    .slice(0, 3);

  return (
    <>
      <div className="statgrid">
        <Stat icon="briefcase" label="Vagas descobertas" n={s?.jobsDiscovered} delta={s ? `+${s.jobsToday} hoje` : ""} />
        <Stat icon="target" label="Matches acima de 75" n={s?.matchesAbove75} delta={s ? `+${s.matchesAbove75Today} hoje` : ""} />
        <Stat icon="chat" label="Mensagens geradas" n={s?.messagesGenerated} delta={s ? `+${s.messagesToday} hoje` : ""} />
        <Stat icon="mail" label="E-mails enviados" n={s?.emailsSent} delta={s ? `+${s.emailsToday} hoje` : ""} />
        <Stat icon="calendar" label="Follow-ups pendentes" n={s?.followUpsPending}
          delta={s?.nextFollowUpInDays != null ? `Próximo: ${s.nextFollowUpInDays} dia(s)` : "—"} mutedDelta />
      </div>

      <div className="cols">
        <div>
          <div className="panel">
            <div className="panel-head">
              <h3>Melhores oportunidades</h3>
              <a onClick={() => onNav("matches")} style={{ cursor: "pointer" }}>Ver todas</a>
            </div>
            {opps.error && <p className="err">{opps.error}</p>}
            {(opps.data ?? []).map((o) => <OppRow key={o.matchId} o={o} />)}
            {opps.data?.length === 0 && <p className="placeholder">Sem matches ainda. Rode uma busca + análise.</p>}
          </div>
        </div>

        <div>
          <div className="panel">
            <div className="panel-head"><h3>Busca ativa</h3></div>
            <p className="sub" style={{ marginTop: -4 }}>Execute uma busca agora mesmo.</p>
            <Action icon="building" t="Buscar por empresa" s="Ex.: Dock, Stone, Nubank" onClick={() => onNav("companies")} />
            <Action icon="tag" t="Buscar por setor" s="Ex.: Fintechs, Bancos" onClick={() => onNav("companies")} />
            <Action icon="search" t="Buscar por palavras-chave" s="Ex.: .NET, C#, AWS, Kafka"
              onClick={async () => { notify("Buscando vagas .NET no Gupy…"); try { await api.search([".net", "c#", "backend", "pagamentos"]); notify("Busca concluída."); onChanged(); } catch { notify("Falha na busca."); } }} />
            <Action icon="link" t="Analisar URL de vaga" s="Cole o link da vaga" onClick={() => notify("Em breve.")} />
            <Action icon="user" t="Analisar recrutador/empresa" s="A partir de um recrutador" onClick={() => notify("Em breve.")} />
          </div>

          <div className="panel">
            <div className="panel-head"><h3>Execuções recentes</h3><a onClick={() => onNav("runs")} style={{ cursor: "pointer" }}>Ver todas</a></div>
            {(runs.data ?? []).map((r) => <RunRow key={r.id} r={r} />)}
            {runs.data?.length === 0 && <p className="placeholder">Nenhuma execução.</p>}
          </div>
        </div>
      </div>

      <div className="panel">
        <div className="panel-head"><h3>Próximos follow-ups</h3><a onClick={() => onNav("followups")} style={{ cursor: "pointer" }}>Ver todas</a></div>
        {upcoming.map((o) => (
          <div className="fu" key={o.id}>
            <Icon name="calendar" />
            <div style={{ flex: 1 }}>Oportunidade <code>{o.id.slice(0, 8)}</code> — <span className="pill">{o.status}</span></div>
            <div className="when">{daysUntil(o.nextFollowUpAtUtc!)}</div>
          </div>
        ))}
        {upcoming.length === 0 && <p className="placeholder">Sem follow-ups agendados.</p>}
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

function OppRow({ o }: { o: BestOpportunity }) {
  return (
    <div className="opp">
      <div className="who">
        <span className="logo" style={{ background: logoColor(o.companyName) }}>{initials(o.companyName)}</span>
        <div>
          <div className="title">{o.jobTitle}</div>
          <div className="chips">{o.skills.map((sk) => <span className="chip" key={sk}>{sk}</span>)}</div>
        </div>
      </div>
      <div className="company">{o.companyName}</div>
      <div className="ring" style={{ borderColor: ringColor(o.overallScore) }}>{o.overallScore}</div>
      <div className={"rec " + recClass(o.recommendation)}>{recLabel(o.recommendation)}</div>
      <a className="btn" href={o.jobUrl} target="_blank" rel="noreferrer">Ver detalhes</a>
    </div>
  );
}

function Action({ icon, t, s, onClick }: { icon: string; t: string; s: string; onClick: () => void }) {
  return (
    <button className="action" onClick={onClick}>
      <span className="ic"><Icon name={icon} /></span>
      <span><span className="t">{t}</span><br /><span className="s">{s}</span></span>
      <span className="arrow">›</span>
    </button>
  );
}

function RunRow({ r }: { r: Run }) {
  const cls = r.status === "Succeeded" ? "ok" : r.status === "Failed" ? "fail" : r.status === "PartiallyFailed" ? "part" : "run";
  return (
    <div className="run">
      <span className={"dot " + cls} />
      <div><div className="t">{runLabel(r.runType)}</div><div className="s">{new Date(r.startedAtUtc).toLocaleString()}</div></div>
      <div className="meta">{r.itemsProcessed} itens · {r.itemsSucceeded} ok</div>
    </div>
  );
}

/* ---------- sub pages ---------- */

function JobsPage({ reload }: { reload: number }) {
  const { data, error } = useAsync(api.jobs, [reload]);
  if (error) return <p className="err">{error}</p>;
  return (
    <div className="panel"><div className="panel-head"><h3>Vagas</h3></div>
      <table><thead><tr><th>Título</th><th>Fonte</th><th>Local</th><th>Status</th><th>Link</th></tr></thead>
        <tbody>{(data ?? []).slice(0, 300).map((j: Job) => (
          <tr key={j.id}><td>{j.title}</td><td>{j.sourceProvider}</td><td>{j.location ?? "—"}</td><td>{j.status}</td>
            <td><a href={j.absoluteUrl} target="_blank" rel="noreferrer">vaga</a></td></tr>
        ))}</tbody></table>
    </div>
  );
}

function CompaniesPage({ reload }: { reload: number }) {
  const { data, error } = useAsync(api.companies, [reload]);
  if (error) return <p className="err">{error}</p>;
  return (
    <div className="panel"><div className="panel-head"><h3>Empresas ({data?.length ?? 0})</h3></div>
      <table><thead><tr><th>Nome</th><th>Prioridade</th><th>Fonte</th><th>Tags</th><th>Board</th></tr></thead>
        <tbody>{(data ?? []).slice(0, 400).map((c: Company) => (
          <tr key={c.id}><td>{c.name}</td><td><span className="pill">{c.priority}</span></td><td>{c.source}</td>
            <td>{c.tags.slice(0, 4).join(", ")}</td>
            <td>{c.careersUrl ? <a href={c.careersUrl} target="_blank" rel="noreferrer">board</a> : "—"}</td></tr>
        ))}</tbody></table>
    </div>
  );
}

function MatchesPage({ reload }: { reload: number }) {
  const { data, error } = useAsync(() => api.bestOpportunities(50), [reload]);
  if (error) return <p className="err">{error}</p>;
  return (
    <div className="panel"><div className="panel-head"><h3>Matches</h3></div>
      {(data ?? []).map((o) => <OppRow key={o.matchId} o={o} />)}
      {data?.length === 0 && <p className="placeholder">Sem matches ainda.</p>}
    </div>
  );
}

function MessagesPage({ reload }: { reload: number }) {
  const { data, error } = useAsync(api.messages, [reload]);
  if (error) return <p className="err">{error}</p>;
  return (
    <div className="panel"><div className="panel-head"><h3>Mensagens geradas</h3></div>
      <table><thead><tr><th>Assunto</th><th>Status</th><th>Criada</th></tr></thead>
        <tbody>{(data ?? []).map((m: Message) => (
          <tr key={m.id}><td>{m.emailSubject}</td><td><span className="pill">{m.status}</span></td>
            <td>{new Date(m.createdAtUtc).toLocaleString()}</td></tr>
        ))}</tbody></table>
      {data?.length === 0 && <p className="placeholder">Nenhuma mensagem gerada.</p>}
    </div>
  );
}

function FollowUpsPage({ reload }: { reload: number }) {
  const { data, error } = useAsync(api.opportunities, [reload]);
  if (error) return <p className="err">{error}</p>;
  const withFu = (data ?? []).filter((o: Opportunity) => o.nextFollowUpAtUtc);
  return (
    <div className="panel"><div className="panel-head"><h3>Follow-ups</h3></div>
      {withFu.map((o) => (
        <div className="fu" key={o.id}><Icon name="calendar" />
          <div style={{ flex: 1 }}>Oportunidade <code>{o.id.slice(0, 8)}</code> — <span className="pill">{o.status}</span></div>
          <div className="when">{daysUntil(o.nextFollowUpAtUtc!)}</div></div>
      ))}
      {withFu.length === 0 && <p className="placeholder">Sem follow-ups agendados.</p>}
    </div>
  );
}

function SearchPage({ notify, onChanged }: { notify: (m: string) => void; onChanged: () => void }) {
  const [kw, setKw] = useState(".net, c#, backend, pagamentos");
  const [busy, setBusy] = useState(false);
  const run = async () => {
    setBusy(true); notify("Buscando…");
    try { await api.search(kw.split(",").map((k) => k.trim()).filter(Boolean)); notify("Busca concluída."); onChanged(); }
    catch { notify("Falha na busca."); } finally { setBusy(false); }
  };
  return (
    <div className="panel"><div className="panel-head"><h3>Busca ativa</h3></div>
      <p className="sub">Busca por palavras-chave (Gupy). Separe por vírgula.</p>
      <input value={kw} onChange={(e) => setKw(e.target.value)}
        style={{ width: "100%", padding: 10, border: "1px solid var(--border)", borderRadius: 8, margin: "10px 0" }} />
      <button className="btn primary" disabled={busy} onClick={run}>{busy ? "Buscando…" : "Buscar vagas"}</button>
    </div>
  );
}

function RunsPage({ reload }: { reload: number }) {
  const { data, error } = useAsync(() => api.runs(50), [reload]);
  if (error) return <p className="err">{error}</p>;
  return (
    <div className="panel"><div className="panel-head"><h3>Execuções</h3></div>
      {(data ?? []).map((r) => <RunRow key={r.id} r={r} />)}
      {data?.length === 0 && <p className="placeholder">Nenhuma execução.</p>}
    </div>
  );
}

function ProfilePage() {
  const { data, error } = useAsync(api.profile, []);
  if (error) return <p className="err">{error}</p>;
  return (
    <div className="panel"><div className="panel-head"><h3>Perfil</h3></div>
      <p><strong>{data?.fullName}</strong></p><p className="sub">{data?.headline}</p></div>
  );
}

function Placeholder({ title }: { title: string }) {
  return <div className="panel"><div className="panel-head"><h3>{title}</h3></div><p className="placeholder">Em breve.</p></div>;
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
function daysUntil(iso: string) {
  const d = Math.round((+new Date(iso) - Date.now()) / 86400000);
  if (d < 0) return `${-d} dia(s) atrás`;
  if (d === 0) return "hoje";
  return `${d} dia(s)`;
}
