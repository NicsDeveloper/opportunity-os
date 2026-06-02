import { useEffect, useRef, useState } from "react";
import { Icon } from "./icons";
import {
  api, type Application, type BestOpportunity, type Company,
  type GeneratedMessage, type Summary,
} from "./api";

type Section = "oportunidades" | "empresas" | "aplicacoes" | "descobertas" | "relatorios";

export function App() {
  const [reload, setReload] = useState(0);
  const [toast, setToast] = useState<string | null>(null);
  const [section, setSection] = useState<Section>("oportunidades");
  const profile = useAsync(api.profile, []);

  const notify = (m: string) => { setToast(m); setTimeout(() => setToast(null), 3500); };
  const refresh = () => setReload((r) => r + 1);

  // Keep the screen fresh while the system works in the background.
  useEffect(() => {
    const id = setInterval(() => setReload((r) => r + 1), 25000);
    return () => clearInterval(id);
  }, []);

  return (
    <div className="layout">
      <Sidebar section={section} setSection={setSection} reload={reload}
        name={profile.data?.fullName} headline={profile.data?.headline} />
      <main className="main">
        <div className="main-inner">
          {section === "oportunidades" && <OpportunitiesScreen reload={reload} notify={notify} onChanged={refresh} firstName={firstNameOf(profile.data?.fullName)} />}
          {section === "empresas" && <CompaniesScreen reload={reload} notify={notify} onChanged={refresh} />}
          {section === "aplicacoes" && <ApplicationsScreen reload={reload} notify={notify} onChanged={refresh} />}
          {section === "descobertas" && <DiscoverScreen reload={reload} notify={notify} onChanged={refresh} />}
          {section === "relatorios" && <ReportsScreen reload={reload} />}
        </div>
      </main>
      {toast && <div className="toast">{toast}</div>}
    </div>
  );
}

/* ============================ sidebar ============================ */

const NAV: { key: Section; label: string; icon: string }[] = [
  { key: "oportunidades", label: "Oportunidades", icon: "shield" },
  { key: "empresas", label: "Empresas", icon: "building" },
  { key: "aplicacoes", label: "Aplicações", icon: "check" },
  { key: "descobertas", label: "Descobertas", icon: "search" },
  { key: "relatorios", label: "Relatórios", icon: "chart" },
];

function Sidebar({ section, setSection, reload, name, headline }: {
  section: Section; setSection: (s: Section) => void; reload: number; name?: string; headline?: string;
}) {
  const runs = useAsync(() => api.runs(10), [reload]);
  const summary = useAsync(api.summary, [reload]);
  const last = runs.data?.[0];
  const points = (runs.data ?? []).map((r) => r.itemsProcessed).reverse();

  return (
    <aside className="sidebar">
      <div className="brand"><span className="mark">◎</span> Opportunity OS</div>

      <nav className="nav">
        {NAV.map((n) => (
          <button key={n.key} className={"nav-item" + (section === n.key ? " active" : "")} onClick={() => setSection(n.key)}>
            <Icon name={n.icon} size={18} /> {n.label}
          </button>
        ))}
      </nav>

      <div className="radar">
        <div className="radar-top">
          <span className="pulse" />
          <span className="radar-t">Radar ativo</span>
        </div>
        <div className="radar-s">Última atualização: {last ? ago(last.startedAtUtc) : "agora"}</div>
        <div className="radar-n"><b>{summary.data?.jobsToday ?? "—"}</b> novas vagas hoje</div>
        <Sparkline points={points} />
      </div>

      <div className="usercard">
        <span className="avatar lg">{initials(name ?? "NS")}</span>
        <div className="uc-body">
          <div className="nm">{name ?? "—"}</div>
          <div className="rl">{headline ?? "Backend Engineer .NET"}</div>
          <button className="link-btn">Editar perfil ›</button>
        </div>
      </div>
    </aside>
  );
}

function Sparkline({ points }: { points: number[] }) {
  if (points.length < 2) return <div className="spark empty" />;
  const max = Math.max(...points, 1);
  const min = Math.min(...points, 0);
  const span = Math.max(max - min, 1);
  const w = 200, h = 44;
  const step = w / (points.length - 1);
  const d = points.map((p, i) => `${i === 0 ? "M" : "L"} ${(i * step).toFixed(1)} ${(h - ((p - min) / span) * (h - 8) - 4).toFixed(1)}`).join(" ");
  return (
    <svg className="spark" viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none">
      <path d={d} fill="none" stroke="var(--accent)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/* ====================== Oportunidades (main) ====================== */

type OppView = "action" | "qualified" | "all";

function OpportunitiesScreen({ reload, notify, onChanged, firstName }: {
  reload: number; notify: (m: string) => void; onChanged: () => void; firstName: string;
}) {
  const [view, setView] = useState<OppView>("action");
  const [region, setRegion] = useState("all");
  const [contract, setContract] = useState("all");
  const [showFilters, setShowFilters] = useState(false);
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);

  const fetcher =
    view === "action" ? () => api.actionToday(region, contract)
    : view === "qualified" ? () => api.qualified(60, region, contract)
    : () => api.allOpportunities(80, region, contract);
  const opps = useAsync(fetcher, [reload, view, region, contract]);

  const last = useAsync(() => api.runs(1), [reload]);
  const all = (opps.data ?? []).filter((o) =>
    !query.trim() || `${o.jobTitle} ${o.companyName}`.toLowerCase().includes(query.trim().toLowerCase()));
  const activeFilters = (region !== "all" ? 1 : 0) + (contract !== "all" ? 1 : 0);

  const runSearch = async () => {
    setBusy(true); notify("Procurando novas vagas .NET…");
    try { await api.search([
      "desenvolvedor .net", "desenvolvedor backend c#", "engenheiro de software .net",
      "vaga .net remoto", "desenvolvedor .net fintech",
    ]); notify("Busca disparada — os resultados chegam aqui em instantes."); onChanged(); }
    catch { notify("Não foi possível buscar agora."); }
    finally { setBusy(false); }
  };

  const heading = view === "action" ? "Para você hoje" : view === "qualified" ? "Boas opções" : "Todas as oportunidades";
  const subtext = view === "action"
    ? "As melhores oportunidades com alta aderência ao seu perfil."
    : view === "qualified" ? "Vagas selecionadas que valem a pena conferir."
    : "Tudo que encontramos recentemente, ordenado pela relevância pra você.";

  return (
    <>
      <header className="hdr">
        <div>
          <h1>{greeting()}, {firstName}! <span className="wave">👋</span></h1>
          <p className="hdr-sub">
            {opps.data
              ? `Encontrei ${all.length} ${all.length === 1 ? "oportunidade excelente" : "oportunidades excelentes"} para você hoje.`
              : "Procurando as melhores oportunidades pra você…"}
          </p>
        </div>
        <div className="hdr-right">
          <span className="updated"><Icon name="refresh" size={15} /> Atualizado {last.data?.[0] ? ago(last.data[0].startedAtUtc) : "agora"}</span>
          <button className="iconbtn" title="Notificações"><Icon name="bell" size={18} /></button>
          <span className="avatar">{initials(firstName)}</span>
        </div>
      </header>

      <div className="section-bar">
        <div className="sb-left">
          <span className="sb-mark"><Icon name="target" size={18} /></span>
          <h2>{heading}</h2>
          <span className="badge">{all.length} {all.length === 1 ? "oportunidade" : "oportunidades"}</span>
        </div>
        <div className="sb-right">
          <div className="searchbox">
            <Icon name="search" size={16} />
            <input placeholder="Buscar vagas…" value={query} onChange={(e) => setQuery(e.target.value)} />
          </div>
          <button className={"btn ghost" + (activeFilters ? " on" : "")} onClick={() => setShowFilters((v) => !v)}>
            <Icon name="filter" size={16} /> Filtros {activeFilters > 0 && <span className="dot-badge">{activeFilters}</span>}
          </button>
        </div>
      </div>
      <p className="sb-sub">{subtext}</p>

      <div className="modebar">
        <Seg value={view} onChange={(v) => setView(v as OppView)} options={[
          ["action", "Para você hoje"], ["qualified", "Boas opções"], ["all", "Todas"]]} />
        <button className="btn primary sm" disabled={busy} onClick={runSearch}>
          {busy ? "Procurando…" : "Procurar vagas"}
        </button>
      </div>

      {showFilters && (
        <div className="filterpanel">
          <span className="flabel">Onde</span>
          <Chips value={region} onChange={setRegion} options={[["all", "Todas"], ["national", "🇧🇷 Brasil"], ["international", "🌎 Exterior"]]} />
          <span className="flabel">Contrato</span>
          <Chips value={contract} onChange={setContract} options={[["all", "Ambos"], ["clt", "CLT"], ["pj", "PJ"]]} />
        </div>
      )}

      <div className="opplist">
        {opps.error && <p className="err">{opps.error}</p>}
        {all.map((o) => <OppCard key={o.matchId} o={o} notify={notify} onChanged={onChanged} />)}
        {opps.data && all.length === 0 && (
          <div className="empty">
            <p>Nada por aqui agora.</p>
            <p className="empty-s">Tente <b>Boas opções</b>, ajuste os filtros, ou toque em <b>Procurar vagas</b>.</p>
          </div>
        )}
        {!opps.data && !opps.error && <CardSkeletons />}
      </div>
    </>
  );
}

/* one opportunity — clean 4-column card */
function OppCard({ o, notify, onChanged }: {
  o: BestOpportunity; notify: (m: string) => void; onChanged: () => void;
}) {
  const [open, setOpen] = useState(false);        // details (chevron)
  const [draft, setDraft] = useState(false);      // draft panel
  const [msg, setMsg] = useState<GeneratedMessage | null>(null);
  const [menu, setMenu] = useState(false);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<{ kind: "applied" | "hidden"; label: string } | null>(null);
  const [why, setWhy] = useState(o.rationale);
  const [score, setScore] = useState(o.overallScore);
  const [analyzing, setAnalyzing] = useState(false);

  const src = sourceLabel(o.sourceType);
  const adh = adherence(score);
  const isTerse = /^score\s+\d+\/100/i.test((why ?? "").trim());

  const act = async (type: string, kind: "applied" | "hidden", label: string) => {
    setMenu(false); setDone({ kind, label });
    try { await api.feedback(type, { jobPostingId: o.jobPostingId }); if (kind === "applied") notify("Movida pra Aplicações ✅"); onChanged(); }
    catch { setDone(null); notify("Não foi possível agora."); }
  };
  const undo = async () => {
    const wasApplied = done?.kind === "applied"; setDone(null);
    if (wasApplied) { try { await api.unapply(o.jobPostingId); } catch { /* ignore */ } onChanged(); }
  };

  const openDraft = async () => {
    if (msg) { setDraft((v) => !v); return; }
    setBusy(true); notify("Gerando rascunho…");
    try { const m = await api.generateOutreach(o.jobPostingId); setMsg(m); setDraft(true); notify("Rascunho pronto — revise e copie."); onChanged(); }
    catch { notify("Não foi possível gerar o rascunho (score abaixo do mínimo?)."); }
    finally { setBusy(false); }
  };
  const analyze = async () => {
    setAnalyzing(true); notify("Analisando aderência…");
    try { const r = await api.analyze(o.jobPostingId); setWhy(r.match.rationale); setScore(r.match.overallScore); notify("Análise concluída."); onChanged(); }
    catch { notify("Não foi possível analisar agora."); }
    finally { setAnalyzing(false); }
  };

  if (done) return (
    <div className={"card done " + done.kind}>
      <span>{done.kind === "applied" ? "✅ Movida pra Aplicações" : "🙈 Ocultada"} — {o.jobTitle}</span>
      <button className="link-btn" onClick={undo}>desfazer</button>
    </div>
  );

  return (
    <div className={"card" + (src.weak ? " weak" : "")}>
      <div className="card-row">
        {/* col 1 — identity */}
        <div className="c-id">
          <Logo name={o.companyName} website={o.companyWebsiteUrl} />
          <div className="id-body">
            <div className="job-title">{o.jobTitle}</div>
            <div className="co-line">
              <span className="co-name">{o.companyName}</span>
              {src.good && <span className="ok-check"><Icon name="check" size={12} /></span>}
              <span className={"src " + src.cls}>{src.text}</span>
              <span className="dotsep">·</span>
              <span className="time">{ago(o.postedAtUtc)}</span>
            </div>
            <div className="tags">{o.skills.slice(0, 5).map((s) => <span className="tag" key={s}>{s}</span>)}</div>
          </div>
        </div>

        {/* col 2 — fit reason */}
        <div className="c-reason">
          <div className={"adh " + adh.tone}><span className="adh-dot" /> {adh.label}</div>
          <p className="reason">{friendlyReason(o, why, isTerse)}</p>
        </div>

        {/* col 3 — score */}
        <div className="c-score">
          <div className={"ring " + ringTone(score)}>{score}</div>
          <div className="score-word">{scoreWord(score)}</div>
          <div className="conf">{confidenceText(o.sourceConfidenceScore, src.weak)}</div>
        </div>

        {/* col 4 — actions */}
        <div className="c-actions">
          <a className="btn primary" href={o.jobUrl} target="_blank" rel="noreferrer">Ver vaga <Icon name="external" size={14} /></a>
          <button className="btn" disabled={busy} onClick={openDraft}>
            <Icon name="edit" size={14} /> {busy ? "Gerando…" : msg ? (draft ? "Ocultar rascunho" : "Ver rascunho") : "Ver rascunho"}
          </button>
          <div className="row-mini">
            <button className="iconbtn sm" title="Detalhes" onClick={() => setOpen((v) => !v)}>
              <span className={"chev" + (open ? " up" : "")}><Icon name="chevron" size={16} /></span>
            </button>
            <div className="menuwrap">
              <button className="iconbtn sm" title="Mais ações" onClick={() => setMenu((v) => !v)}><Icon name="more" size={16} /></button>
              {menu && (
                <>
                  <div className="menu-scrim" onClick={() => setMenu(false)} />
                  <div className="menu">
                    <button onClick={() => act("Applied", "applied", "já me cadastrei")}>✓ Já me cadastrei</button>
                    <button onClick={() => act("HideSimilar", "hidden", "ocultada")}>🙈 Ocultar</button>
                    <button onClick={() => act("Irrelevant", "hidden", "irrelevante")}>👎 Marcar irrelevante</button>
                    <button onClick={() => act("BadCompanyDetection", "hidden", "empresa errada")}>🏢 Empresa errada</button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </div>

      {open && (
        <div className="card-details">
          <div className="cd-why"><strong>Por que combina:</strong> {why || "Sem detalhes ainda."}</div>
          {isTerse && <button className="link-btn" disabled={analyzing} onClick={analyze}>{analyzing ? "Analisando…" : "↻ Analisar aderência em detalhe"}</button>}
        </div>
      )}

      {msg && draft && <DraftPanel msg={msg} notify={notify} />}
    </div>
  );
}

function DraftPanel({ msg, notify }: { msg: GeneratedMessage; notify: (m: string) => void }) {
  return (
    <div className="draft">
      <p className="draft-note"><Icon name="user" size={14} /> Rascunho para revisão — nada é enviado automaticamente.</p>
      <Copy label="Mensagem (LinkedIn / direta)" text={msg.linkedInMessage} notify={notify} />
      <Copy label="Assunto do e-mail" text={msg.emailSubject} notify={notify} single />
      <Copy label="Corpo do e-mail" text={msg.emailBody} notify={notify} />
      {msg.followUpMessage && <Copy label="Follow-up (depois)" text={msg.followUpMessage} notify={notify} />}
    </div>
  );
}
function Copy({ label, text, notify, single }: { label: string; text: string; notify: (m: string) => void; single?: boolean }) {
  const copy = async () => { try { await navigator.clipboard.writeText(text); notify(`Copiado: ${label}`); } catch { notify("Não foi possível copiar."); } };
  return (
    <div className="copy">
      <div className="copy-head"><span>{label}</span><button className="btn xs" onClick={copy}><Icon name="copy" size={13} /> Copiar</button></div>
      {single ? <div className="copy-single">{text}</div> : <pre className="copy-text">{text}</pre>}
    </div>
  );
}

/* ============================ Empresas ============================ */

function CompaniesScreen({ reload, notify, onChanged }: { reload: number; notify: (m: string) => void; onChanged: () => void }) {
  const companies = useAsync(api.companies, [reload]);
  const [q, setQ] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const list = (companies.data ?? [])
    .filter((c) => !c.tags?.includes("do-not-promote"))
    .filter((c) => !q.trim() || c.name.toLowerCase().includes(q.trim().toLowerCase()))
    .slice(0, 60);

  const refreshCompany = async (c: Company) => {
    setBusy(c.id); notify(`Atualizando busca em ${c.name}…`);
    try { await api.discover(c.id); notify(`Busca atualizada para ${c.name}.`); onChanged(); }
    catch { notify("Não foi possível atualizar agora."); }
    finally { setBusy(null); }
  };

  return (
    <>
      <SimpleHeader title="Empresas" sub="Busque uma empresa e atualize a procura de vagas dela." />
      <div className="searchbox big">
        <Icon name="search" size={18} />
        <input placeholder="Buscar empresa… (ex.: Stone, BRQ, BTG, Dock)" value={q} onChange={(e) => setQ(e.target.value)} />
      </div>
      <div className="complist">
        {list.map((c) => (
          <div className="comp" key={c.id}>
            <Logo name={c.name} website={c.websiteUrl} />
            <div className="comp-body">
              <div className="comp-name">{c.name}</div>
              <div className="comp-meta">
                <span className={"prio " + c.priority.toLowerCase()}>{priorityLabel(c.priority)}</span>
                <span className="dotsep">·</span>
                <span className="comp-mapped">{c.websiteUrl ? "site mapeado" : "site a mapear"}{c.careersUrl ? " · carreiras ok" : ""}</span>
              </div>
            </div>
            <button className="btn" disabled={busy !== null} onClick={() => refreshCompany(c)}>
              {busy === c.id ? "Atualizando…" : "Atualizar busca"}
            </button>
          </div>
        ))}
        {companies.data && list.length === 0 && <div className="empty"><p>Nenhuma empresa encontrada.</p></div>}
        {!companies.data && <CardSkeletons rows={4} />}
      </div>
    </>
  );
}

/* ============================ Aplicações ============================ */

function ApplicationsScreen({ reload, notify, onChanged }: { reload: number; notify: (m: string) => void; onChanged: () => void }) {
  const apps = useAsync(api.applications, [reload]);
  const all = apps.data ?? [];
  const undo = async (a: Application) => {
    try { await api.unapply(a.jobPostingId); notify(`Voltou pro mural: ${a.jobTitle}`); onChanged(); }
    catch { notify("Não foi possível desfazer."); }
  };
  return (
    <>
      <SimpleHeader title="Aplicações" sub="O que você já tratou. Some do mural principal pra você focar no que falta." />
      <div className="opplist">
        {apps.error && <p className="err">{apps.error}</p>}
        {all.map((a) => (
          <div className="card appitem" key={a.jobPostingId}>
            <div className="c-id">
              <Logo name={a.companyName} website={a.companyWebsiteUrl} />
              <div className="id-body">
                <div className="job-title">{a.jobTitle}</div>
                <div className="co-line">
                  <span className="co-name">{a.companyName}</span>
                  <span className="dotsep">·</span>
                  <span className="app-tag">{actionLabel(a.action)}</span>
                  <span className="dotsep">·</span>
                  <span className="time">marcada {ago(a.appliedAtUtc)}</span>
                </div>
              </div>
            </div>
            {a.overallScore > 0 && <div className={"ring sm " + ringTone(a.overallScore)}>{a.overallScore}</div>}
            <div className="c-actions">
              <a className="btn primary" href={a.jobUrl} target="_blank" rel="noreferrer">Ver vaga <Icon name="external" size={14} /></a>
              <button className="btn" onClick={() => undo(a)}>↩ Reabrir</button>
            </div>
          </div>
        ))}
        {apps.data && all.length === 0 && (
          <div className="empty"><p>Nada por aqui ainda.</p>
            <p className="empty-s">Marque uma vaga como <b>“Já me cadastrei”</b> no mural e ela aparece aqui.</p></div>
        )}
        {!apps.data && <CardSkeletons rows={3} />}
      </div>
    </>
  );
}

/* ============================ Descobertas ============================ */

function DiscoverScreen({ reload, notify, onChanged }: { reload: number; notify: (m: string) => void; onChanged: () => void }) {
  const preview = useAsync(() => api.bacenPreview("High"), [reload]);
  const candidates = useAsync(() => api.consultingCandidates(40), [reload]);
  const campaigns = useAsync(api.campaigns, [reload]);
  const [busy, setBusy] = useState<string | null>(null);
  const p = preview.data;

  const run = async (key: string, label: string, fn: () => Promise<unknown>) => {
    setBusy(key); notify(`${label}…`);
    try { await fn(); notify(`${label}: pronto! Os resultados entram nas oportunidades.`); onChanged(); }
    catch { notify(`Não foi possível: ${label}.`); }
    finally { setBusy(null); }
  };
  const promote = async (id: string, name: string) => {
    try { const r = await api.promoteConsulting(id); notify(r.promoted ? `Empresa salva: ${name}` : "Ainda sem confiança pra salvar."); onChanged(); }
    catch { notify("Não foi possível salvar a empresa."); }
  };

  return (
    <>
      <SimpleHeader title="Descobertas" sub="Amplie a busca em fontes que combinam com você." />

      <div className="disc">
        <div className="disc-head">
          <div><h3>🏦 Bancos e fintechs</h3>
            <p>{p ? `${p.companies} instituições no radar (${p.strategic} estratégicas, ${p.high} prioritárias).` : "Carregando…"}</p></div>
          <button className="btn primary" disabled={busy !== null} onClick={() => run("bacen", "Procurando em bancos e fintechs", api.runBacenSweep)}>
            {busy === "bacen" ? "Procurando…" : "Procurar vagas"}
          </button>
        </div>
      </div>

      <div className="disc">
        <div className="disc-head">
          <div><h3>🧩 Consultorias de tecnologia</h3>
            <p>Empresas que vivem de contratar dev .NET/C#.</p></div>
          <button className="btn primary" disabled={busy !== null} onClick={() => run("consulting", "Procurando consultorias", api.runConsultingDiscover)}>
            {busy === "consulting" ? "Procurando…" : "Procurar consultorias"}
          </button>
        </div>
        <div className="disc-list">
          {(candidates.data ?? []).slice(0, 8).map((c) => (
            <div className="disc-item" key={c.id}>
              <div><span className="di-name">{c.name}</span>
                {c.consultingConfidenceScore >= 70 && <span className="ok-pill">boa aposta</span>}
                <div className="di-sub">{c.signals.slice(0, 3).join(" · ")}</div></div>
              {c.status === "PromotedToCompany"
                ? <span className="ok-pill">salva</span>
                : <button className="btn" disabled={c.consultingConfidenceScore < 70} onClick={() => promote(c.id, c.name)}>Salvar empresa</button>}
            </div>
          ))}
          {candidates.data?.length === 0 && <p className="muted-line">Nenhuma ainda. Toque em “Procurar consultorias”.</p>}
        </div>
      </div>

      <div className="disc">
        <div className="disc-head"><div><h3>🔁 Buscas salvas</h3><p>Conjuntos prontos pra ampliar quando quiser.</p></div></div>
        <div className="disc-list">
          {(campaigns.data ?? []).map((c) => (
            <div className="disc-item" key={c.id}>
              <div><span className="di-name">{c.name}</span><div className="di-sub">{c.description}</div></div>
              <button className="btn" disabled={busy !== null} onClick={() => run("camp-" + c.id, `Rodando “${c.name}”`, () => api.runCampaign(c.id))}>
                {busy === "camp-" + c.id ? "Rodando…" : "Rodar"}</button>
            </div>
          ))}
          {campaigns.data?.length === 0 && <p className="muted-line">Sem buscas salvas.</p>}
        </div>
      </div>
    </>
  );
}

/* ============================ Relatórios ============================ */

function ReportsScreen({ reload }: { reload: number }) {
  const s = useAsync(api.summary, [reload]) as { data: Summary | null };
  const companies = useAsync(api.companies, [reload]);
  const apps = useAsync(api.applications, [reload]);
  const d = s.data;
  return (
    <>
      <SimpleHeader title="Relatórios" sub="Um panorama rápido do que o seu radar produziu." />
      <div className="report-grid">
        <ReportStat label="Boas vagas pra você" n={d?.matchesAbove75} sub={d ? `+${d.matchesAbove75Today} hoje` : ""} />
        <ReportStat label="Vagas encontradas" n={d?.jobsDiscovered} sub={d ? `+${d.jobsToday} hoje` : ""} />
        <ReportStat label="Empresas no radar" n={companies.data?.length} sub="monitoradas" />
        <ReportStat label="Aplicações" n={apps.data?.length} sub="que você já tratou" />
        <ReportStat label="Rascunhos prontos" n={d?.messagesGenerated} sub={d ? `+${d.messagesToday} hoje` : ""} />
        <ReportStat label="Lembretes" n={d?.followUpsPending} sub={d?.nextFollowUpInDays != null ? `próximo em ${d.nextFollowUpInDays} dia(s)` : "—"} />
      </div>
    </>
  );
}
function ReportStat({ label, n, sub }: { label: string; n?: number; sub: string }) {
  return <div className="rstat"><div className="rs-n">{n ?? "…"}</div><div className="rs-l">{label}</div><div className="rs-s">{sub}</div></div>;
}

/* ============================ shared bits ============================ */

function SimpleHeader({ title, sub }: { title: string; sub: string }) {
  return <header className="hdr"><div><h1>{title}</h1><p className="hdr-sub">{sub}</p></div></header>;
}

function Seg({ value, onChange, options }: { value: string; onChange: (v: string) => void; options: [string, string][] }) {
  return (
    <div className="seg">
      {options.map(([v, l]) => <button key={v} className={value === v ? "active" : ""} onClick={() => onChange(v)}>{l}</button>)}
    </div>
  );
}
function Chips({ value, onChange, options }: { value: string; onChange: (v: string) => void; options: [string, string][] }) {
  return <span className="chips-g">{options.map(([v, l]) => <button key={v} className={"chip-b" + (value === v ? " active" : "")} onClick={() => onChange(v)}>{l}</button>)}</span>;
}
function CardSkeletons({ rows = 4 }: { rows?: number }) {
  return <>{Array.from({ length: rows }).map((_, i) => <div className="card skel" key={i} />)}</>;
}

function Logo({ name, website }: { name: string; website?: string | null }) {
  const [i, setI] = useState(0);
  const host = hostOf(website);
  const sources = host ? [`https://www.google.com/s2/favicons?sz=64&domain=${host}`] : [];
  if (i < sources.length) return <img className="logo img" alt={name} src={sources[i]} onError={() => setI(i + 1)} />;
  return <span className="logo" style={{ background: logoColor(name) }}>{initials(name)}</span>;
}

/* ---------- helpers ---------- */
function useAsync<T>(fn: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const fnRef = useRef(fn); fnRef.current = fn;
  useEffect(() => {
    let active = true; setError(null);
    fnRef.current().then((d) => active && setData(d)).catch((e) => active && setError(String(e)));
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);
  return { data, error };
}
function firstNameOf(full?: string) { return (full ?? "").trim().split(" ")[0] || "Nícolas"; }
function hostOf(url?: string | null) { if (!url) return null; try { return new URL(url).host; } catch { return null; } }
function greeting() { const h = new Date().getHours(); return h < 12 ? "Bom dia" : h < 18 ? "Boa tarde" : "Boa noite"; }
function initials(name: string) { const p = name.trim().split(/\s+/); return ((p[0]?.[0] ?? "") + (p[1]?.[0] ?? "")).toUpperCase() || "?"; }
function logoColor(name: string) { let h = 0; for (const c of name) h = (h * 31 + c.charCodeAt(0)) % 360; return `hsl(${h} 50% 48%)`; }

function ringTone(s: number) { return s >= 80 ? "good" : s >= 60 ? "warn" : "muted"; }
function scoreWord(s: number) { return s >= 85 ? "Excelente" : s >= 70 ? "Boa" : "Média"; }
function adherence(s: number) {
  if (s >= 85) return { label: "Excelente aderência", tone: "good" };
  if (s >= 70) return { label: "Boa aderência", tone: "good" };
  return { label: "Vale atenção", tone: "warn" };
}
function confidenceText(conf?: number, weak?: boolean) {
  if (weak) return "Fonte externa";
  if ((conf ?? 0) >= 70) return "Confiança alta";
  if ((conf ?? 0) >= 40) return "Confiança média";
  return "Fonte externa";
}
function sourceLabel(t?: string): { text: string; cls: string; good?: boolean; weak?: boolean } {
  switch (t) {
    case "OfficialAts":
    case "OfficialCareerPage": return { text: "Site oficial", cls: "good", good: true };
    case "SocialIndexed": return { text: "LinkedIn", cls: "ext" };
    case "JobBoard": return { text: "Fonte externa", cls: "ext" };
    case "Aggregator": return { text: "Agregador", cls: "weak", weak: true };
    default: return { text: "Fonte externa", cls: "ext" };
  }
}
function friendlyReason(o: BestOpportunity, why: string, terse: boolean) {
  if (why && !terse) return why.length > 160 ? why.slice(0, 157) + "…" : why;
  const sk = o.skills.filter((s) => /\.net|c#|backend|azure|aws|fintech|pagament/i.test(s)).slice(0, 2);
  const base = sk.length ? sk.join(" e ") : ".NET/C#";
  return `Forte match com seu perfil em ${base} e backend.`;
}
function actionLabel(a: string) { return a === "ContactedRecruiter" ? "contatei recrutador" : "cadastrei/apliquei"; }
function priorityLabel(p: string) {
  const m: Record<string, string> = { Strategic: "estratégica", High: "prioritária", Medium: "no radar", Low: "baixa" };
  return m[p] ?? p.toLowerCase();
}
function ago(iso: string) {
  const days = Math.floor((Date.now() - +new Date(iso)) / 86400000);
  if (days <= 0) {
    const h = Math.floor((Date.now() - +new Date(iso)) / 3600000);
    if (h <= 0) { const mn = Math.floor((Date.now() - +new Date(iso)) / 60000); return mn <= 1 ? "há pouco" : `há ${mn} min`; }
    return `há ${h}h`;
  }
  if (days < 30) return `há ${days} dia(s)`;
  if (days < 365) return `há ${Math.floor(days / 30)} mês(es)`;
  return `há ${Math.floor(days / 365)} ano(s)`;
}
