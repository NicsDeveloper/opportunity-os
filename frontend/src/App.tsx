import { useEffect, useRef, useState, type ReactNode } from "react";
import { Icon } from "./icons";
import {
  api, ApiError, type Application, type AuthMe, type BestOpportunity, type CandidateProfileDraft,
  type Company, type GeneratedMessage, type Profile, type ProfileInput, type Summary,
  type UploadLinkedInPdfResponse,
} from "./api";

type Section = "oportunidades" | "empresas" | "aplicacoes" | "descobertas" | "relatorios" | "perfis" | "admin";

// The selected candidate profile lives client-side (localStorage) — no global server "active" state.
const PROFILE_KEY = "oos.selectedProfileId";

// Top-level auth gate: undefined = checking session, null = logged out, AuthMe = logged in.
export function App() {
  const [me, setMe] = useState<AuthMe | null | undefined>(undefined);

  useEffect(() => { api.auth.me().then(setMe).catch(() => setMe(null)); }, []);

  if (me === undefined)
    return <div className="auth-shell"><div className="auth-card"><div className="auth-brand">Opportunity OS</div><p className="auth-sub">Carregando…</p></div></div>;
  if (me === null)
    return <AuthScreen onAuthed={setMe} />;

  const logout = async () => {
    try { await api.auth.logout(); } catch { /* ignore */ }
    localStorage.removeItem(PROFILE_KEY);
    setMe(null);
  };
  return <Workspace me={me} onLogout={logout} />;
}

function Workspace({ me, onLogout }: { me: AuthMe; onLogout: () => void }) {
  const [reload, setReload] = useState(0);
  const [toast, setToast] = useState<string | null>(null);
  const [section, setSection] = useState<Section>("oportunidades");
  const [selectedProfileId, setSelectedProfileId] = useState<string | null>(() => localStorage.getItem(PROFILE_KEY));
  const profiles = useAsync(api.profiles, [reload]);

  const list = profiles.data ?? [];
  // The persisted selection must belong to THIS user; otherwise fall back to default/first.
  const current = list.find((p) => p.id === selectedProfileId) ?? list.find((p) => p.isDefault) ?? list[0];

  const selectProfile = (id: string) => { setSelectedProfileId(id); localStorage.setItem(PROFILE_KEY, id); };

  const notify = (m: string) => { setToast(m); setTimeout(() => setToast(null), 3500); };
  const refresh = () => setReload((r) => r + 1);

  // Keep the screen fresh while the system works in the background.
  useEffect(() => {
    const id = setInterval(() => setReload((r) => r + 1), 25000);
    return () => clearInterval(id);
  }, []);

  // No profile yet → onboarding (import LinkedIn PDF or fill manually), then the feed appears.
  if (profiles.data && list.length === 0)
    return <OnboardingGate displayName={me.displayName} notify={notify} onDone={(id) => { selectProfile(id); refresh(); }} onLogout={onLogout} />;

  return (
    <div className="layout">
      <Sidebar section={section} setSection={setSection} reload={reload}
        profiles={list} current={current} onSelectProfile={selectProfile}
        userName={me.displayName} userEmail={me.email} isAdmin={me.isAdmin} onLogout={onLogout} />
      <main className="main">
        <div className="main-inner">
          {section === "oportunidades" && <OpportunitiesScreen reload={reload} notify={notify} onChanged={refresh} firstName={firstNameOf(current?.fullName)} profileLabel={current?.displayName} profileId={current?.id} />}
          {section === "empresas" && <CompaniesScreen reload={reload} notify={notify} onChanged={refresh} />}
          {section === "aplicacoes" && <ApplicationsScreen reload={reload} notify={notify} onChanged={refresh} profileId={current?.id} />}
          {section === "descobertas" && <DiscoverScreen reload={reload} notify={notify} onChanged={refresh} />}
          {section === "relatorios" && <ReportsScreen reload={reload} profileId={current?.id} />}
          {section === "perfis" && <ProfilesScreen reload={reload} notify={notify} onChanged={refresh} selectedId={current?.id} onSelectProfile={selectProfile} />}
          {section === "admin" && me.isAdmin && <AdminScreen reload={reload} notify={notify} onChanged={refresh} />}
        </div>
      </main>
      {toast && <div className="toast">{toast}</div>}
    </div>
  );
}

/* ============================ auth & onboarding ============================ */

function AuthScreen({ onAuthed }: { onAuthed: (me: AuthMe) => void }) {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    if (!email.trim() || !password) { setError("Informe e-mail e senha."); return; }
    if (mode === "register" && password.length < 8) { setError("A senha precisa de ao menos 8 caracteres."); return; }
    setBusy(true);
    try {
      const me = mode === "login"
        ? await api.auth.login(email.trim(), password)
        : await api.auth.register(email.trim(), password, name.trim() || undefined);
      onAuthed(me);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Não foi possível continuar. Tente novamente.");
    } finally { setBusy(false); }
  };

  return (
    <div className="auth-shell">
      <div className="auth-card">
        <div className="auth-brand">Opportunity OS</div>
        <p className="auth-sub">
          {mode === "login" ? "Entre para ver seu radar de oportunidades." : "Crie sua conta e monte seu radar."}
        </p>
        <div className="auth-form" onKeyDown={(e) => { if (e.key === "Enter") submit(); }}>
          {mode === "register" && (
            <label className="field"><span className="field-l">Nome</span>
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Como te chamamos" autoFocus /></label>
          )}
          <label className="field"><span className="field-l">E-mail</span>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="voce@email.com" autoFocus={mode === "login"} /></label>
          <label className="field"><span className="field-l">Senha</span>
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="••••••••" /></label>
          {error && <p className="err">{error}</p>}
          <button className="btn primary" disabled={busy} onClick={submit}>
            {busy ? "…" : mode === "login" ? "Entrar" : "Criar conta"}
          </button>
        </div>
        <p className="auth-switch">
          {mode === "login"
            ? <>Não tem conta? <button className="link-btn" onClick={() => { setMode("register"); setError(null); }}>Criar conta</button></>
            : <>Já tem conta? <button className="link-btn" onClick={() => { setMode("login"); setError(null); }}>Entrar</button></>}
        </p>
      </div>
    </div>
  );
}

function OnboardingGate({ displayName, notify, onDone, onLogout }: {
  displayName: string; notify: (m: string) => void; onDone: (profileId: string) => void; onLogout: () => void;
}) {
  const [mode, setMode] = useState<"choice" | "manual" | "import">("choice");

  if (mode === "manual")
    return <Onboarding displayName={displayName} notify={notify} onDone={onDone} onLogout={onLogout} />;
  if (mode === "import")
    return <LinkedInImport notify={notify} onDone={onDone} onManual={() => setMode("manual")} onLogout={onLogout} />;

  return (
    <div className="auth-shell">
      <div className="auth-card wide">
        <div className="auth-brand">Crie seu perfil profissional</div>
        <p className="auth-sub">Escolha como começar — você poderá revisar e editar tudo depois.</p>
        <div className="onb-choices">
          <button className="onb-choice rec" onClick={() => setMode("import")}>
            <span className="onb-choice-tag">Recomendado</span>
            <strong>Importar PDF do LinkedIn</strong>
            <span>Use o PDF exportado do seu LinkedIn para preencher seu perfil automaticamente.</span>
          </button>
          <button className="onb-choice" onClick={() => setMode("manual")}>
            <strong>Preencher manualmente</strong>
            <span>Informe suas skills, cargos desejados e preferências em poucos passos.</span>
          </button>
        </div>
        <p className="onb-privacy">
          Seu PDF será usado apenas para montar seu perfil profissional dentro do Opportunity OS.
          Você poderá revisar tudo antes de salvar.
        </p>
        <div className="pf-actions"><button className="link-btn onb-logout" onClick={onLogout}>Sair</button></div>
      </div>
    </div>
  );
}

function LinkedInImport({ notify, onDone, onManual, onLogout }: {
  notify: (m: string) => void; onDone: (profileId: string) => void; onManual: () => void; onLogout: () => void;
}) {
  const [state, setState] = useState<"idle" | "uploading" | "review" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [resp, setResp] = useState<UploadLinkedInPdfResponse | null>(null);
  const [draft, setDraft] = useState<CandidateProfileDraft | null>(null);
  const [busy, setBusy] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);

  const set = (patch: Partial<CandidateProfileDraft>) => setDraft((d) => (d ? { ...d, ...patch } : d));

  const upload = async (file?: File | null) => {
    if (!file) return;
    setState("uploading"); setError(null);
    try {
      const r = await api.profileImports.uploadLinkedInPdf(file);
      setResp(r); setDraft(r.draft); setState("review");
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Falha ao processar o PDF.");
      setState("error");
    }
  };

  const save = async () => {
    if (!resp || !draft) return;
    if (!draft.displayName.trim()) { notify("Dê um nome ao perfil."); return; }
    setBusy(true);
    try {
      const created = await api.profileImports.apply(resp.importId, draft, true);
      notify("Perfil criado a partir do LinkedIn.");
      onDone(created.id);
    } catch (e) { notify("Não foi possível salvar: " + (e instanceof ApiError ? e.message : String(e))); setBusy(false); }
  };

  if (state === "review" && draft) {
    return (
      <div className="auth-shell">
        <div className="auth-card wide review">
          <div className="auth-brand">Revise seu perfil</div>
          <p className="onb-disclaimer">⚠ Revise antes de salvar. O sistema pode errar skills, senioridade ou experiências.</p>
          {(resp?.warnings ?? []).map((w, i) => <p key={i} className="onb-warning">{w}</p>)}

          <div className="pf-grid">
            <Field label="Nome do perfil (rótulo)"><input value={draft.displayName} onChange={(e) => set({ displayName: e.target.value })} /></Field>
            <Field label="Headline"><input value={draft.headline} onChange={(e) => set({ headline: e.target.value })} /></Field>
            <Field label="Senioridade"><input value={draft.seniority} onChange={(e) => set({ seniority: e.target.value })} /></Field>
            <Field label="Localização"><input value={draft.location} onChange={(e) => set({ location: e.target.value })} /></Field>
            <Field label="Core skills (vírgula)"><input value={draft.coreSkills.join(", ")} onChange={(e) => set({ coreSkills: splitCsv(e.target.value) })} /></Field>
            <Field label="Secondary skills (vírgula)"><input value={draft.secondarySkills.join(", ")} onChange={(e) => set({ secondarySkills: splitCsv(e.target.value) })} /></Field>
            <Field label="Domínios (vírgula)"><input value={draft.domains.join(", ")} onChange={(e) => set({ domains: splitCsv(e.target.value) })} /></Field>
            <Field label="Cargos desejados (vírgula)"><input value={draft.preferredRoles.join(", ")} onChange={(e) => set({ preferredRoles: splitCsv(e.target.value) })} /></Field>
            <Field label="Modelos de trabalho (vírgula)"><input value={draft.preferredWorkModes.join(", ")} onChange={(e) => set({ preferredWorkModes: splitCsv(e.target.value) })} /></Field>
          </div>

          {draft.experiences.length > 0 && (
            <div className="review-exp">
              <div className="field-l">Experiências detectadas (revise)</div>
              {draft.experiences.map((x, i) => (
                <div key={i} className="exp-row"><strong>{x.role || "(cargo)"}</strong> · {x.company || "(empresa)"} <span className="muted">{x.period}</span></div>
              ))}
            </div>
          )}

          <div className="pf-actions">
            <button className="btn primary" disabled={busy} onClick={save}>{busy ? "Salvando…" : "Salvar perfil"}</button>
            <button className="btn" onClick={onManual}>Editar manualmente</button>
            <button className="btn ghost" onClick={() => { setState("idle"); setResp(null); setDraft(null); }}>Enviar outro PDF</button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="auth-shell">
      <div className="auth-card wide">
        <div className="auth-brand">Importar perfil do LinkedIn</div>
        <p className="auth-sub">
          Envie o PDF exportado pelo LinkedIn. Vamos extrair suas experiências, skills, resumo e formação
          para montar seu perfil inicial. Você revisa tudo antes de salvar.
        </p>
        <input ref={fileRef} type="file" accept="application/pdf,.pdf" style={{ display: "none" }}
          onChange={(e) => upload(e.target.files?.[0])} />
        {state !== "uploading" && (
          <button className="btn primary" onClick={() => fileRef.current?.click()}>Selecionar PDF</button>
        )}
        {state === "uploading" && <p className="auth-sub">Analisando seu perfil do LinkedIn…</p>}
        {state === "error" && error && <p className="err">{error}</p>}
        <p className="auth-switch">
          Prefere outro caminho? <button className="link-btn" onClick={onManual}>Preencher manualmente</button>
          {"  ·  "}<button className="link-btn" onClick={onLogout}>Sair</button>
        </p>
      </div>
    </div>
  );
}

const ONBOARDING_STEPS = [
  "Quem é você profissionalmente?",
  "Quais stacks você quer priorizar?",
  "Que tipo de oportunidade você quer ver?",
  "Ver oportunidades",
];

function Onboarding({ displayName, notify, onDone, onLogout }: {
  displayName: string; notify: (m: string) => void; onDone: (profileId: string) => void; onLogout: () => void;
}) {
  const [step, setStep] = useState(0);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState<ProfileInput>({
    ...EMPTY_FORM, fullName: displayName, displayName: "",
  });
  const set = (patch: Partial<ProfileInput>) => setForm((f) => ({ ...f, ...patch }));

  const canNext =
    step === 0 ? !!(form.displayName?.trim() && form.headline.trim() && form.seniority.trim())
    : step === 1 ? (form.coreSkills?.length ?? 0) > 0
    : true;

  const finish = async () => {
    setBusy(true);
    try {
      const created = await api.createProfile({ ...form, fullName: form.fullName || form.displayName || "Meu perfil" });
      notify("Perfil criado. Buscando oportunidades…");
      onDone(created.id);
    } catch (e) {
      notify("Não foi possível criar o perfil: " + (e instanceof ApiError ? e.message : String(e)));
      setBusy(false);
    }
  };

  return (
    <div className="auth-shell">
      <div className="auth-card wide">
        <div className="auth-brand">Vamos montar seu radar</div>
        <div className="onb-steps">
          {ONBOARDING_STEPS.map((_, i) => (
            <span key={i} className={"onb-dot" + (i === step ? " active" : i < step ? " done" : "")}>{i + 1}</span>
          ))}
        </div>
        <h2 className="onb-title">{ONBOARDING_STEPS[step]}</h2>

        {step === 0 && (
          <div className="pf-grid">
            <Field label="Nome do perfil (rótulo)"><input autoFocus value={form.displayName ?? ""} onChange={(e) => set({ displayName: e.target.value })} placeholder="Ex.: Backend .NET" /></Field>
            <Field label="Cargo desejado / headline"><input value={form.headline} onChange={(e) => set({ headline: e.target.value })} placeholder="Ex.: Desenvolvedor Backend .NET" /></Field>
            <Field label="Senioridade"><input value={form.seniority} onChange={(e) => set({ seniority: e.target.value })} placeholder="Pleno/Sênior" /></Field>
          </div>
        )}
        {step === 1 && (
          <div className="pf-grid">
            <Field label="Principais skills (vírgula)"><input autoFocus value={(form.coreSkills ?? []).join(", ")} onChange={(e) => set({ coreSkills: splitCsv(e.target.value) })} placeholder="C#, .NET, SQL" /></Field>
            <Field label="Skills secundárias (vírgula)"><input value={(form.secondarySkills ?? []).join(", ")} onChange={(e) => set({ secondarySkills: splitCsv(e.target.value) })} placeholder="Azure, Docker" /></Field>
            <Field label="Cargos desejados (vírgula)"><input value={(form.preferredRoles ?? []).join(", ")} onChange={(e) => set({ preferredRoles: splitCsv(e.target.value) })} placeholder="Backend, Tech Lead" /></Field>
          </div>
        )}
        {step === 2 && (
          <div className="pf-grid">
            <Field label="Localização"><input autoFocus value={form.location} onChange={(e) => set({ location: e.target.value })} placeholder="São Paulo / Remoto" /></Field>
            <Field label="Modelos de trabalho (vírgula)"><input value={(form.preferredWorkModes ?? []).join(", ")} onChange={(e) => set({ preferredWorkModes: splitCsv(e.target.value) })} placeholder="Remoto, Híbrido" /></Field>
            <Field label="Idioma"><input value={form.preferredLanguage} onChange={(e) => set({ preferredLanguage: e.target.value })} placeholder="pt-BR" /></Field>
          </div>
        )}
        {step === 3 && (
          <p className="onb-recap">
            Tudo pronto, <strong>{form.displayName || displayName}</strong>. Vamos criar seu perfil e mostrar as oportunidades mais aderentes.
          </p>
        )}

        <div className="pf-actions">
          {step > 0 && <button className="btn" onClick={() => setStep(step - 1)} disabled={busy}>‹ Voltar</button>}
          {step < ONBOARDING_STEPS.length - 1
            ? <button className="btn primary" onClick={() => setStep(step + 1)} disabled={!canNext}>Continuar ›</button>
            : <button className="btn primary" onClick={finish} disabled={busy}>{busy ? "Criando…" : "Ver oportunidades"}</button>}
          <button className="link-btn onb-logout" onClick={onLogout}>Sair</button>
        </div>
      </div>
    </div>
  );
}

/* ============================ sidebar ============================ */

const NAV: { key: Section; label: string; icon: string }[] = [
  { key: "oportunidades", label: "Oportunidades", icon: "shield" },
  { key: "empresas", label: "Empresas", icon: "building" },
  { key: "aplicacoes", label: "Aplicações", icon: "check" },
];
const SYS_NAV: { key: Section; label: string; icon: string }[] = [
  { key: "descobertas", label: "Descobertas", icon: "search" },
  { key: "relatorios", label: "Relatórios", icon: "chart" },
  { key: "perfis", label: "Perfis", icon: "building" },
];

function Sidebar({ section, setSection, reload, profiles, current, onSelectProfile, userName, userEmail, isAdmin, onLogout }: {
  section: Section; setSection: (s: Section) => void; reload: number;
  profiles: Profile[]; current?: Profile; onSelectProfile: (id: string) => void;
  userName: string; userEmail: string; isAdmin: boolean; onLogout: () => void;
}) {
  const runs = useAsync(() => api.runs(3), [reload]);
  const summary = useAsync(() => api.summary(current?.id), [reload, current?.id]);
  const last = runs.data?.[0];

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
        <div className="radar-n"><b>{summary.data?.jobsToday ?? "—"}</b> vagas novas hoje</div>
      </div>

      <nav className="nav sys">
        <div className="nav-label">Sistema</div>
        {SYS_NAV.map((n) => (
          <button key={n.key} className={"nav-item small" + (section === n.key ? " active" : "")} onClick={() => setSection(n.key)}>
            <Icon name={n.icon} size={16} /> {n.label}
          </button>
        ))}
        {isAdmin && (
          <button className={"nav-item small" + (section === "admin" ? " active" : "")} onClick={() => setSection("admin")}>
            <Icon name="bolt" size={16} /> Operação
          </button>
        )}
      </nav>

      <div className="usercard">
        <span className="avatar lg">{initials(current?.displayName ?? current?.fullName ?? "NS")}</span>
        <div className="uc-body">
          <div className="uc-label">Perfil ativo</div>
          {profiles.length > 0 ? (
            <select className="profile-select" value={current?.id ?? ""} onChange={(e) => onSelectProfile(e.target.value)}>
              {profiles.map((p) => (
                <option key={p.id} value={p.id}>{p.displayName}{p.isDefault ? " ★" : ""}</option>
              ))}
            </select>
          ) : <div className="nm">—</div>}
          <div className="rl">{current?.headline ?? "Backend Engineer .NET"}</div>
          <button className="link-btn" onClick={() => setSection("perfis")}>Gerenciar perfis ›</button>
        </div>
      </div>

      <div className="account">
        <div className="acc-id">
          <div className="acc-name">{userName}</div>
          <div className="acc-email">{userEmail}</div>
        </div>
        <button className="link-btn" onClick={onLogout}>Sair</button>
      </div>
    </aside>
  );
}

/* ====================== Oportunidades (main) ====================== */

// Friendly, jargon-free narration of what the search is doing (Firehose/Serper/provider stay hidden).
const SEARCH_STEPS = [
  "Procurando vagas .NET nos portais (Gupy, Recrutei, Infojobs, Vagas.com)…",
  "Vasculhando a web por vagas novas e recentes…",
  "Identificando a empresa e a fonte de cada vaga…",
  "Avaliando a aderência ao seu perfil .NET/C#…",
];

function SearchProgress({ prog }: { prog: { step: number; done?: { n: number; u: number; s: number; e: number } } }) {
  const done = prog.done;
  return (
    <div className={"searchprog" + (done ? " ok" : "")}>
      <div className="sp-head">
        {done
          ? <><span className="sp-check">✓</span> Busca concluída</>
          : <><span className="sp-pulse" /> Procurando vagas pra você…</>}
      </div>
      {done ? (
        <div className="sp-result">
          {done.n > 0
            ? <><b>{done.n}</b> {done.n === 1 ? "vaga nova" : "vagas novas"}</>
            : "Nenhuma vaga nova desta vez"}
          {done.u > 0 && <> · <b>{done.u}</b> atualizadas</>}
          {" · em "}<b>{done.s}</b> {done.s === 1 ? "fonte" : "fontes"}
          {done.e > 0 && <span className="sp-warn"> · {done.e} com aviso</span>}
        </div>
      ) : (
        <ul className="sp-steps">
          {SEARCH_STEPS.map((s, i) => (
            <li key={i} className={i < prog.step ? "done" : i === prog.step ? "active" : ""}>
              <span className="sp-mark">{i < prog.step ? "✓" : i === prog.step ? <span className="sp-spin" /> : "○"}</span>
              {s}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function OpportunitiesScreen({ reload, notify, onChanged, firstName, profileLabel, profileId }: {
  reload: number; notify: (m: string) => void; onChanged: () => void; firstName: string; profileLabel?: string; profileId?: string;
}) {
  const [region, setRegion] = useState("all");
  const [contract, setContract] = useState("all");
  const [sort, setSort] = useState<"score" | "recent">("score");
  const [showFilters, setShowFilters] = useState(false);
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const [prog, setProg] = useState<{ step: number; done?: { n: number; u: number; s: number; e: number } } | null>(null);
  const [page, setPage] = useState(0);

  // Fit-to-screen: show exactly as many cards as fit the viewport so the user never scrolls.
  const listRef = useRef<HTMLDivElement>(null);
  const [pageSize, setPageSize] = useState(5);
  useEffect(() => {
    const calc = () => {
      const top = listRef.current?.getBoundingClientRect().top ?? 280;
      const fit = Math.floor((window.innerHeight - top - 110) / 146); // ~card h incl gap; reserve pager + padding
      setPageSize(Math.max(3, Math.min(8, fit)));
    };
    calc();
    window.addEventListener("resize", calc);
    return () => window.removeEventListener("resize", calc);
  }, [showFilters]);

  // One direct feed: the best matches for the profile (learned prefs already applied server-side),
  // ordered by adherence. No tabs — weak sources just sort to the bottom.
  const opps = useAsync(() => api.qualified(500, region, contract, profileId), [reload, region, contract, profileId]);
  const last = useAsync(() => api.runs(1), [reload]);

  const all = (opps.data ?? [])
    .filter((o) => !query.trim() || `${o.jobTitle} ${o.companyName}`.toLowerCase().includes(query.trim().toLowerCase()))
    .sort(sort === "recent"
      // Mais recentes: vagas com data de publicação real primeiro, por data desc; depois o resto.
      ? (a, b) => (Number(!!b.datePrecise) - Number(!!a.datePrecise)) || (+new Date(b.postedAtUtc) - +new Date(a.postedAtUtc))
      // Aderência (padrão): score desc; fontes fracas empatadas ficam abaixo.
      : (a, b) => (b.overallScore - a.overallScore) || (Number(isWeakSource(a)) - Number(isWeakSource(b))));
  const activeFilters = (region !== "all" ? 1 : 0) + (contract !== "all" ? 1 : 0) + (sort !== "score" ? 1 : 0);
  const pageCount = Math.max(1, Math.ceil(all.length / pageSize));
  const current = Math.min(page, pageCount - 1);
  const shown = all.slice(current * pageSize, current * pageSize + pageSize);
  // Reset to page 1 only when the user changes filters/search — never on a background
  // refresh (which would yank the user off the page they're reading).
  useEffect(() => { setPage(0); }, [region, contract, query, sort, profileId]);

  const runSearch = async () => {
    if (busy) return;
    setBusy(true); setProg({ step: 0 });
    // Advance the friendly steps while the request is in flight (the API call is synchronous).
    const timer = window.setInterval(
      () => setProg((p) => (p && !p.done ? { ...p, step: Math.min(p.step + 1, SEARCH_STEPS.length - 1) } : p)), 1400);
    try {
      const r = await api.search([
        "desenvolvedor .net", "desenvolvedor backend c#", "engenheiro de software .net",
        "vaga .net remoto", "desenvolvedor .net fintech",
      ]);
      window.clearInterval(timer);
      setProg({ step: SEARCH_STEPS.length, done: { n: r.jobsDiscovered, u: r.jobsUpdated, s: r.providersInvoked, e: r.errors } });
      onChanged();
      window.setTimeout(() => setProg(null), 7000);
    } catch {
      window.clearInterval(timer);
      setProg(null); notify("Não foi possível buscar agora.");
    } finally { setBusy(false); }
  };

  return (
    <>
      <header className="hdr">
        <div>
          <h1>{greeting()}, {firstName}! <span className="wave">👋</span></h1>
          <p className="hdr-sub">
            {opps.data
              ? `Encontrei ${all.length} ${all.length === 1 ? "oportunidade" : "oportunidades"} com boa aderência ${profileLabel ? `ao perfil ${profileLabel}` : "ao seu perfil"}.`
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
          <h2>Oportunidades pra você</h2>
          <span className="badge">{all.length}</span>
        </div>
        <div className="sb-right">
          <div className="searchbox">
            <Icon name="search" size={16} />
            <input placeholder="Buscar vagas…" value={query} onChange={(e) => setQuery(e.target.value)} />
          </div>
          <button className={"btn ghost" + (activeFilters ? " on" : "")} onClick={() => setShowFilters((v) => !v)}>
            <Icon name="filter" size={16} /> Filtros {activeFilters > 0 && <span className="dot-badge">{activeFilters}</span>}
          </button>
          <button className="btn primary sm" disabled={busy} onClick={runSearch}>
            <Icon name="bolt" size={15} /> {busy ? "Buscando…" : "Buscar agora"}
          </button>
        </div>
      </div>
      <p className="sb-sub">As melhores oportunidades, ordenadas pela aderência ao seu perfil.</p>

      {showFilters && (
        <div className="filterpanel">
          <span className="flabel">Onde</span>
          <Chips value={region} onChange={setRegion} options={[["all", "Todas"], ["national", "🇧🇷 Brasil"], ["international", "🌎 Exterior"]]} />
          <span className="flabel">Contrato</span>
          <Chips value={contract} onChange={setContract} options={[["all", "Todos"], ["clt", "CLT"], ["pj", "PJ"], ["both", "Ambos"], ["unknown", "Não informado"]]} />
          <span className="flabel">Ordenar</span>
          <Chips value={sort} onChange={(v) => setSort(v as "score" | "recent")} options={[["score", "Aderência"], ["recent", "Recentes"]]} />
        </div>
      )}

      {prog && <SearchProgress prog={prog} />}

      <div className="opplist" ref={listRef}>
        {opps.error && <p className="err">{opps.error}</p>}
        {shown.map((o) => <OppCard key={o.matchId} o={o} notify={notify} onChanged={onChanged} profileId={profileId} />)}
        {opps.data && all.length === 0 && (
          <div className="empty">
            <p>Nenhuma vaga com esses filtros.</p>
            <p className="empty-s">
              {contract === "pj" ? "Poucas vagas declaram PJ explicitamente — a maioria não informa o contrato."
                : contract === "clt" ? "Poucas vagas declaram CLT explicitamente — a maioria não informa o contrato."
                : contract === "both" ? "Poucas vagas dizem oferecer CLT e PJ."
                : activeFilters > 0 ? "Tente afrouxar os filtros (ex.: Contrato “Todos”)."
                : <>Toque em <b>Buscar agora</b> pra trazer vagas novas.</>}
            </p>
          </div>
        )}
        {!opps.data && !opps.error && <CardSkeletons />}
      </div>

      {all.length > pageSize && (
        <div className="pager">
          <button className="btn" disabled={current === 0} onClick={() => setPage(current - 1)}>‹ Anterior</button>
          <span className="pager-info">Página {current + 1} de {pageCount} · {all.length} vagas</span>
          <button className="btn" disabled={current >= pageCount - 1} onClick={() => setPage(current + 1)}>Próxima ›</button>
        </div>
      )}
    </>
  );
}

/* one opportunity — clean 4-column card */
function OppCard({ o, notify, onChanged, profileId }: {
  o: BestOpportunity; notify: (m: string) => void; onChanged: () => void; profileId?: string;
}) {
  const [open, setOpen] = useState(false);        // details (chevron)
  const [draft, setDraft] = useState(false);      // draft panel
  const [msg, setMsg] = useState<GeneratedMessage | null>(null);
  const [menu, setMenu] = useState(false);
  const [busy, setBusy] = useState(false);
  const [leaving, setLeaving] = useState(false);
  const [asking, setAsking] = useState(false);
  const [reason, setReason] = useState("");
  const [done, setDone] = useState<{ kind: "applied" | "hidden"; label: string } | null>(null);
  const [why, setWhy] = useState(o.rationale);
  const [score, setScore] = useState(o.overallScore);
  const [analyzing, setAnalyzing] = useState(false);

  const src = sourceLabel(o.sourceType);
  const adh = adherence(score);
  const titleParts = cleanTitle(o.jobTitle);
  const isTerse = /^score\s+\d+\/100/i.test((why ?? "").trim());

  // Fire the feedback (with optional reason the system learns from), play the leave
  // animation, then show the slim undo row.
  const act = (type: string, kind: "applied" | "hidden", label: string, why?: string) => {
    setMenu(false); setAsking(false);
    setLeaving(true);
    api.feedback(type, { jobPostingId: o.jobPostingId, reason: why?.trim() || undefined, candidateProfileId: profileId })
      .then(() => {
        if (kind === "applied") notify("Movida pra Aplicações ✅");
        else if (why?.trim()) notify("Anotado — o radar vai aprender 👍");
      })
      .catch(() => notify("Não foi possível agora."));
    window.setTimeout(() => setDone({ kind, label }), 280);
  };
  const dismissWithReason = (r: string) => { setReason(""); act("Irrelevant", "hidden", "irrelevante", r); };
  const undo = async () => {
    const wasApplied = done?.kind === "applied";
    setDone(null); setLeaving(false);
    try { await api.unapply(o.jobPostingId, profileId); } catch { /* ignore */ }
    if (wasApplied) onChanged();
  };

  const openDraft = async () => {
    if (msg) { setDraft((v) => !v); return; }
    setBusy(true); notify("Gerando rascunho…");
    try { const m = await api.generateOutreach(o.jobPostingId, profileId); setMsg(m); setDraft(true); notify("Rascunho pronto — revise e copie."); onChanged(); }
    catch { notify("Não foi possível gerar o rascunho (score abaixo do mínimo?)."); }
    finally { setBusy(false); }
  };
  const analyze = async () => {
    setAnalyzing(true); notify("Analisando aderência…");
    try { const r = await api.analyze(o.jobPostingId, profileId); setWhy(r.match.rationale); setScore(r.match.overallScore); notify("Análise concluída."); onChanged(); }
    catch { notify("Não foi possível analisar agora."); }
    finally { setAnalyzing(false); }
  };

  if (done) return (
    <div className={"card done in " + done.kind}>
      <span>{done.kind === "applied" ? "✅ Movida pra Aplicações" : "🙈 Removida do mural"} — {o.jobTitle}</span>
      <button className="link-btn" onClick={undo}>desfazer</button>
    </div>
  );

  return (
    <div className={"card oppcard" + (src.weak ? " weak" : "") + (leaving ? " leaving" : "") + (menu ? " menu-open" : "")}>
      <button className="card-x" title="Não serve — dizer por quê" onClick={() => { setMenu(false); setAsking(true); }}>
        <Icon name="close" size={13} />
      </button>
      <div className="card-row">
        {/* col 1 — identity */}
        <div className="c-id">
          <Logo name={o.companyName} website={o.companyWebsiteUrl} />
          <div className="id-body">
            <div className="co-line">
              <span className="co-name">{o.companyName}</span>
              {src.good && <span className="ok-check"><Icon name="check" size={12} /></span>}
              <span className={"src " + src.cls}>{src.text}</span>
              <span className="dotsep">·</span>
              <span className={"time" + (o.datePrecise ? "" : " approx")} title={o.datePrecise ? "data de publicação da vaga" : "quando o radar encontrou (data de publicação desconhecida)"}>
                {(o.datePrecise ? "publicada " : "encontrada ") + ago(o.postedAtUtc)}
              </span>
            </div>
            <div className="job-title">{titleParts.title}</div>
            {titleParts.sub && <div className="job-sub">{titleParts.sub}</div>}
            <div className="tags">{o.skills.slice(0, 5).map((s) => <span className="tag" key={s}>{s}</span>)}</div>
          </div>
        </div>

        {/* col 2 — fit reason */}
        <div className="c-reason">
          <div className={"adh " + adh.tone}><span className="adh-dot" /> {adh.label}</div>
          <p className="reason">{friendlyReason(o)}</p>
        </div>

        {/* col 3 — score */}
        <div className="c-score">
          <div className={"ring " + ringTone(score)}>{score}</div>
          <div className="score-word">{scoreAction(score)}</div>
          <div className="conf">{confidenceText(o.sourceConfidenceScore, src.weak)}</div>
        </div>

        {/* col 4 — actions */}
        <div className="c-actions">
          <a className="btn primary xs" href={o.jobUrl} target="_blank" rel="noreferrer"><Icon name="external" size={13} /> Ver vaga</a>
          <div className="act2">
            <button className="btn xs" disabled={busy} onClick={openDraft}>{busy ? "…" : draft ? "Ocultar" : "Rascunho"}</button>
            <button className="btn xs ok" onClick={() => act("Applied", "applied", "feito")}>Feito</button>
          </div>
        </div>

        {/* col 5 — secondary actions */}
        <div className="c-more menuwrap">
          <button className="kebab" title="Mais ações" onClick={() => setMenu((v) => !v)}><Icon name="more" size={18} /></button>
          {menu && (
            <>
              <div className="menu-scrim" onClick={() => setMenu(false)} />
              <div className="menu">
                <button onClick={() => { setMenu(false); setOpen((v) => !v); }}>ℹ️ Ver detalhes</button>
                <button onClick={() => { setMenu(false); setAsking(true); }}>👎 Não serve (dizer por quê)</button>
                <button onClick={() => act("HideSimilar", "hidden", "ocultada")}>🙈 Só ocultar</button>
                <button onClick={() => act("BadCompanyDetection", "hidden", "empresa errada")}>🏢 Empresa errada</button>
              </div>
            </>
          )}
        </div>
      </div>

      {asking && (
        <div className="reasonbox">
          <div className="reason-q">Por que não serve? <span>ajuda o radar a aprender e parar de sugerir parecidas</span></div>
          <div className="reason-chips">
            {["Empresa", "Stack diferente", "Sênior demais", "Júnior demais", "Internacional", "Presencial", "Não é .NET"].map((c) => (
              <button key={c} onClick={() => dismissWithReason(c)}>{c}</button>
            ))}
          </div>
          <div className="reason-row">
            <input autoFocus placeholder="Escreva o motivo (opcional)…" value={reason}
              onChange={(e) => setReason(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter") dismissWithReason(reason); }} />
            <button className="btn xs primary" onClick={() => dismissWithReason(reason)}>Confirmar</button>
            <button className="btn xs" onClick={() => { setAsking(false); setReason(""); }}>Cancelar</button>
          </div>
        </div>
      )}

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

function ApplicationsScreen({ reload, notify, onChanged, profileId }: { reload: number; notify: (m: string) => void; onChanged: () => void; profileId?: string }) {
  const apps = useAsync(() => api.applications(profileId), [reload, profileId]);
  const all = apps.data ?? [];
  const undo = async (a: Application) => {
    try { await api.unapply(a.jobPostingId, profileId); notify(`Voltou pro mural: ${a.jobTitle}`); onChanged(); }
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

function ReportsScreen({ reload, profileId }: { reload: number; profileId?: string }) {
  const s = useAsync(() => api.summary(profileId), [reload, profileId]) as { data: Summary | null };
  const companies = useAsync(api.companies, [reload]);
  const apps = useAsync(() => api.applications(profileId), [reload, profileId]);
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

/* ============================ Perfis ============================ */

const EMPTY_FORM: ProfileInput = {
  fullName: "", displayName: "", headline: "", summary: "", location: "", seniority: "Pleno/Sênior",
  preferredLanguage: "pt-BR", coreSkills: [], secondarySkills: [], excludedStacks: [], domains: [],
  preferredRoles: [], preferredContractTypes: [], preferredLocations: [], preferredWorkModes: [],
};

function toForm(p: Profile): ProfileInput {
  return {
    fullName: p.fullName, displayName: p.displayName, headline: p.headline, summary: p.summary,
    location: p.location, seniority: p.seniority, preferredLanguage: p.preferredLanguage,
    coreSkills: p.coreSkills, secondarySkills: p.secondarySkills, excludedStacks: p.excludedStacks,
    domains: p.domains, preferredRoles: p.preferredRoles, preferredContractTypes: p.preferredContractTypes,
    preferredLocations: p.preferredLocations, preferredWorkModes: p.preferredWorkModes,
    minimumScoreToShow: p.minimumScoreToShow,
  };
}

function ProfilesScreen({ reload, notify, onChanged, selectedId, onSelectProfile }: {
  reload: number; notify: (m: string) => void; onChanged: () => void;
  selectedId?: string; onSelectProfile: (id: string) => void;
}) {
  const profiles = useAsync(api.profiles, [reload]);
  const list = profiles.data ?? [];
  // null = not editing; "new" = creating; otherwise the profile id being edited.
  const [editing, setEditing] = useState<string | null>(null);
  const [form, setForm] = useState<ProfileInput>(EMPTY_FORM);
  const [busy, setBusy] = useState(false);

  const startNew = () => { setForm({ ...EMPTY_FORM }); setEditing("new"); };
  const startEdit = (p: Profile) => { setForm(toForm(p)); setEditing(p.id); };

  const save = async () => {
    if (!form.fullName.trim()) { notify("Informe ao menos o nome."); return; }
    setBusy(true);
    try {
      if (editing === "new") {
        const created = await api.createProfile(form);
        onSelectProfile(created.id);
        notify("Perfil criado.");
      } else if (editing) {
        await api.updateProfile(editing, form);
        notify("Perfil atualizado.");
      }
      setEditing(null);
      onChanged();
    } catch (e) { notify("Não foi possível salvar: " + String(e)); }
    finally { setBusy(false); }
  };

  const makeDefault = async (id: string) => {
    setBusy(true);
    try { await api.setDefaultProfile(id); notify("Perfil padrão atualizado."); onChanged(); }
    catch (e) { notify("Falha ao definir padrão: " + String(e)); }
    finally { setBusy(false); }
  };

  return (
    <>
      <header className="hdr">
        <div>
          <h1>Perfis</h1>
          <p className="hdr-sub">Crie e edite perfis de candidato. O motor recomenda vagas por perfil.</p>
        </div>
        <div className="hdr-right">
          <button className="btn primary sm" onClick={startNew}><Icon name="bolt" size={15} /> Novo perfil</button>
        </div>
      </header>

      <div className="profiles-grid">
        {profiles.error && <p className="err">{profiles.error}</p>}
        {!profiles.data && <CardSkeletons rows={3} />}
        {list.map((p) => (
          <div key={p.id} className={"card profile-card" + (p.id === selectedId ? " active" : "")}>
            <div className="pc-top">
              <span className="avatar">{initials(p.displayName || p.fullName)}</span>
              <div className="pc-id">
                <div className="nm">{p.displayName} {p.isDefault && <span className="tag">padrão</span>}</div>
                <div className="rl">{p.headline}</div>
              </div>
            </div>
            <div className="pc-skills">{(p.coreSkills ?? []).slice(0, 6).map((s) => <span className="chip" key={s}>{s}</span>)}</div>
            <div className="pc-actions">
              <button className="btn sm" onClick={() => onSelectProfile(p.id)}>Selecionar</button>
              <button className="btn sm" onClick={() => startEdit(p)}>Editar</button>
              {!p.isDefault && <button className="btn sm ghost" disabled={busy} onClick={() => makeDefault(p.id)}>Tornar padrão</button>}
            </div>
          </div>
        ))}
      </div>

      {editing && (
        <div className="profile-form card">
          <h2>{editing === "new" ? "Novo perfil" : "Editar perfil"}</h2>
          <div className="pf-grid">
            <Field label="Nome do perfil (rótulo)"><input value={form.displayName ?? ""} onChange={(e) => setForm({ ...form, displayName: e.target.value })} placeholder="Ex.: Java Backend" /></Field>
            <Field label="Nome completo"><input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} /></Field>
            <Field label="Headline"><input value={form.headline} onChange={(e) => setForm({ ...form, headline: e.target.value })} /></Field>
            <Field label="Senioridade"><input value={form.seniority} onChange={(e) => setForm({ ...form, seniority: e.target.value })} /></Field>
            <Field label="Localização"><input value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} /></Field>
            <Field label="Idioma"><input value={form.preferredLanguage} onChange={(e) => setForm({ ...form, preferredLanguage: e.target.value })} /></Field>
            <Field label="Core skills (vírgula)"><input value={(form.coreSkills ?? []).join(", ")} onChange={(e) => setForm({ ...form, coreSkills: splitCsv(e.target.value) })} /></Field>
            <Field label="Secondary skills (vírgula)"><input value={(form.secondarySkills ?? []).join(", ")} onChange={(e) => setForm({ ...form, secondarySkills: splitCsv(e.target.value) })} /></Field>
            <Field label="Domínios (vírgula)"><input value={(form.domains ?? []).join(", ")} onChange={(e) => setForm({ ...form, domains: splitCsv(e.target.value) })} /></Field>
            <Field label="Cargos desejados (vírgula)"><input value={(form.preferredRoles ?? []).join(", ")} onChange={(e) => setForm({ ...form, preferredRoles: splitCsv(e.target.value) })} /></Field>
            <Field label="Tipos de contrato (vírgula)"><input value={(form.preferredContractTypes ?? []).join(", ")} onChange={(e) => setForm({ ...form, preferredContractTypes: splitCsv(e.target.value) })} /></Field>
            <Field label="Modelos de trabalho (vírgula)"><input value={(form.preferredWorkModes ?? []).join(", ")} onChange={(e) => setForm({ ...form, preferredWorkModes: splitCsv(e.target.value) })} /></Field>
          </div>
          <div className="pf-actions">
            <button className="btn primary" disabled={busy} onClick={save}>{busy ? "Salvando…" : "Salvar"}</button>
            <button className="btn" onClick={() => setEditing(null)}>Cancelar</button>
          </div>
        </div>
      )}
    </>
  );
}

/* ============================ admin (operação) ============================ */

function AdminScreen({ reload, notify, onChanged }: {
  reload: number; notify: (m: string) => void; onChanged: () => void;
}) {
  const ov = useAsync(() => api.admin.overview(), [reload]);
  const [maxCompanies, setMaxCompanies] = useState(50);
  const [seconds, setSeconds] = useState(120);
  const [busy, setBusy] = useState(false);
  const data = ov.data;

  const runSweep = async () => {
    setBusy(true);
    try {
      await api.admin.sweep(maxCompanies || undefined, seconds);
      notify(`Varredura iniciada (até ${maxCompanies || "todas"} empresas, máx ${seconds}s).`);
      window.setTimeout(onChanged, 2000);
    } catch (e) { notify("Falha ao iniciar varredura: " + String(e)); }
    finally { setBusy(false); }
  };

  return (
    <>
      <header className="hdr">
        <div>
          <h1>Operação</h1>
          <p className="hdr-sub">Painel do administrador — captura de vagas e status do radar.</p>
        </div>
        <div className="hdr-right"><button className="btn sm" onClick={onChanged}>Atualizar</button></div>
      </header>

      {ov.error && <p className="err">{ov.error}</p>}

      <div className="admin-cards">
        <div className="card admin-stat"><div className="as-n">{data?.companies ?? "—"}</div><div className="as-l">Empresas cadastradas</div></div>
        <div className="card admin-stat"><div className="as-n">{data?.scannable ?? "—"}</div><div className="as-l">Com careers (varreável)</div></div>
        <div className="card admin-stat"><div className="as-n">{data?.jobs ?? "—"}</div><div className="as-l">Vagas capturadas</div></div>
        <div className="card admin-stat"><div className="as-n">{data?.matches ?? "—"}</div><div className="as-l">Matches</div></div>
      </div>

      <div className="card admin-sweep">
        <h2>Rodar varredura agora</h2>
        <p className="hdr-sub">Varre as empresas cadastradas (prioridade primeiro), em segundo plano. Limite por nº de empresas e/ou tempo.</p>
        <div className="pf-grid">
          <Field label="Máx. empresas (0 = todas)">
            <input type="number" min={0} value={maxCompanies} onChange={(e) => setMaxCompanies(Math.max(0, Number(e.target.value) || 0))} />
          </Field>
          <Field label="Tempo máximo (segundos)">
            <input type="number" min={5} max={1800} value={seconds} onChange={(e) => setSeconds(Math.min(1800, Math.max(5, Number(e.target.value) || 120)))} />
          </Field>
        </div>
        <div className="pf-actions">
          <button className="btn primary" disabled={busy} onClick={runSweep}>{busy ? "Iniciando…" : "▶ Rodar varredura"}</button>
        </div>
      </div>

      <div className="card">
        <h2>Captura contínua (Worker)</h2>
        <p className="hdr-sub">Jobs recorrentes que mantêm a captura rodando enquanto o Worker está ativo.</p>
        <table className="admin-table">
          <thead><tr><th>Job</th><th>Cron</th><th>O que faz</th></tr></thead>
          <tbody>{(data?.recurring ?? []).map((r) => (
            <tr key={r.name}><td>{r.name}</td><td><code>{r.cron}</code></td><td>{r.desc}</td></tr>
          ))}</tbody>
        </table>
      </div>

      <div className="card">
        <h2>Execuções recentes</h2>
        <table className="admin-table">
          <thead><tr><th>Tipo</th><th>Status</th><th>Proc.</th><th>OK</th><th>Erros</th><th>Início</th></tr></thead>
          <tbody>{(data?.runs ?? []).map((r) => (
            <tr key={r.id}>
              <td>{r.runType}</td>
              <td><span className={"run-status " + r.status.toLowerCase()}>{r.status}</span></td>
              <td>{r.itemsProcessed}</td><td>{r.itemsSucceeded}</td><td>{r.itemsFailed}</td>
              <td>{ago(r.startedAtUtc)}</td>
            </tr>
          ))}</tbody>
        </table>
      </div>
    </>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="field"><span className="field-l">{label}</span>{children}</label>;
}
function splitCsv(v: string): string[] {
  return v.split(",").map((s) => s.trim()).filter(Boolean);
}

/* ============================ shared bits ============================ */

function SimpleHeader({ title, sub }: { title: string; sub: string }) {
  return <header className="hdr"><div><h1>{title}</h1><p className="hdr-sub">{sub}</p></div></header>;
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
function scoreAction(s: number) { return s >= 90 ? "Abrir primeiro" : s >= 80 ? "Vale olhar" : s >= 75 ? "Boa opção" : "Vale conferir"; }

// Clean noisy titles into a bold main title + a soft subtitle.
// "Pessoa Desenvolvedor .Net Pleno | Vagas 100% remotas" -> { title: "Desenvolvedor .Net Pleno", sub: "Vagas 100% remotas" }
function cleanTitle(raw: string): { title: string; sub?: string } {
  let t = (raw ?? "").trim();
  // drop trailing source/board noise ("- Gupy", "- Recrutei", "| JobLeads.com", "- Vaga de emprego ...")
  t = t.replace(/\s*[-|–·]\s*(gupy|recrutei|adzuna|jobleads(\.com)?|buscojobs|jobrapido|simplyhired|jobgether|remotejobs|remoteleaf|himalayas|caderno nacional|vaga de emprego.*|p[áa]gina da vaga.*)\s*$/i, "");
  // strip leading filler
  t = t.replace(/^\s*(pessoa\s+|vaga\s+(para|de)\s+|wanted:\s*|oportunidade:\s*|nova vaga\s*\|?\s*)/i, "");
  // split into segments and pick the first that looks like a real title (skip codes like "11251", "#5241")
  const parts = t.split(/\s*[|–·]\s*|\s+-\s+/).map((p) => p.trim()).filter(Boolean);
  let idx = parts.findIndex((p) => /[a-zà-ú]/i.test(p) && p.replace(/[^a-zà-ú]/gi, "").length >= 3);
  if (idx < 0) idx = 0;
  const title = (parts[idx] || t).replace(/\s{2,}/g, " ").trim();
  const sub = parts.slice(idx + 1).join(" · ").trim() || undefined;
  return { title: title.length > 64 ? title.slice(0, 62) + "…" : title, sub: sub && sub.length <= 48 ? sub : undefined };
}
function adherence(s: number) {
  if (s >= 85) return { label: "Excelente aderência", tone: "good" };
  if (s >= 70) return { label: "Boa aderência", tone: "good" };
  return { label: "Vale atenção", tone: "warn" };
}
function confidenceText(conf?: number, weak?: boolean) {
  if (weak) return "Encontrada na web";
  if ((conf ?? 0) >= 70) return "Confiança alta";
  if ((conf ?? 0) >= 40) return "Confiança média";
  return "Encontrada na web";
}
function sourceLabel(t?: string): { text: string; cls: string; good?: boolean; weak?: boolean } {
  switch (t) {
    case "OfficialAts":
    case "OfficialCareerPage": return { text: "Site oficial", cls: "good", good: true };
    case "SocialIndexed": return { text: "LinkedIn", cls: "ext" };
    case "JobBoard": return { text: "Encontrada na web", cls: "ext" };
    case "Aggregator": return { text: "Fonte menos confiável", cls: "weak", weak: true };
    default: return { text: "Encontrada na web", cls: "ext" };
  }
}
function isWeakSource(o: BestOpportunity) {
  return o.sourceType === "Aggregator" || (o.sourceConfidenceScore ?? 0) < 40;
}
// Truthful, profile-agnostic one-liner: reflects the JOB's real stack (not a hardcoded ".NET/C#").
function friendlyReason(o: BestOpportunity) {
  const top = (o.skills ?? []).map((s) => s.trim()).filter(Boolean).slice(0, 4);
  return top.length ? `Combina pela stack: ${top.join(", ")}.` : "Veja os detalhes da aderência ao seu perfil.";
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
