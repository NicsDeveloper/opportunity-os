import { useEffect, useState } from "react";
import { api, type Company, type Job, type Opportunity, type DigestPreview } from "./api";

type Tab = "overview" | "companies" | "jobs" | "opportunities" | "digest";

export function App() {
  const [tab, setTab] = useState<Tab>("overview");
  const tabs: [Tab, string][] = [
    ["overview", "Overview"], ["companies", "Empresas"], ["jobs", "Vagas"],
    ["opportunities", "Oportunidades"], ["digest", "Digest"],
  ];
  return (
    <div className="app">
      <header>
        <h1>Opportunity OS</h1>
        <div className="sub">Radar de oportunidades — revisão humana</div>
      </header>
      <nav>
        {tabs.map(([id, label]) => (
          <button key={id} className={tab === id ? "active" : ""} onClick={() => setTab(id)}>{label}</button>
        ))}
      </nav>
      {tab === "overview" && <Overview />}
      {tab === "companies" && <Companies />}
      {tab === "jobs" && <Jobs />}
      {tab === "opportunities" && <Opportunities />}
      {tab === "digest" && <Digest />}
    </div>
  );
}

function useAsync<T>(fn: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let active = true;
    fn().then((d) => active && setData(d)).catch((e) => active && setError(String(e)));
    return () => { active = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);
  return { data, error };
}

function Overview() {
  const companies = useAsync(api.companies);
  const jobs = useAsync(api.jobs);
  const opps = useAsync(api.opportunities);
  const err = companies.error ?? jobs.error ?? opps.error;
  if (err) return <p className="err">Erro ao carregar: {err}. A API está rodando?</p>;
  const followUps = (opps.data ?? []).filter((o) => o.nextFollowUpAtUtc && new Date(o.nextFollowUpAtUtc) <= new Date());
  const card = (n: number | string, l: string) => (
    <div className="card"><div className="n">{n}</div><div className="l">{l}</div></div>
  );
  return (
    <div className="cards">
      {card(companies.data?.length ?? "…", "Empresas")}
      {card(jobs.data?.length ?? "…", "Vagas")}
      {card(opps.data?.length ?? "…", "Oportunidades")}
      {card(followUps.length, "Follow-ups pendentes")}
    </div>
  );
}

function Companies() {
  const { data, error } = useAsync(api.companies);
  if (error) return <p className="err">{error}</p>;
  return (
    <table>
      <thead><tr><th>Nome</th><th>Prioridade</th><th>Fonte</th><th>Tags</th><th>Careers</th></tr></thead>
      <tbody>
        {(data ?? []).map((c: Company) => (
          <tr key={c.id}>
            <td>{c.name}</td>
            <td><span className="pill">{c.priority}</span></td>
            <td>{c.source}</td>
            <td>{c.tags.slice(0, 4).join(", ")}</td>
            <td>{c.careersUrl ? <a href={c.careersUrl} target="_blank" rel="noreferrer">board</a> : "—"}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Jobs() {
  const { data, error } = useAsync(api.jobs);
  if (error) return <p className="err">{error}</p>;
  return (
    <table>
      <thead><tr><th>Título</th><th>Fonte</th><th>Local</th><th>Status</th><th>Link</th></tr></thead>
      <tbody>
        {(data ?? []).slice(0, 300).map((j: Job) => (
          <tr key={j.id}>
            <td>{j.title}</td>
            <td>{j.sourceProvider}</td>
            <td>{j.location ?? "—"}</td>
            <td>{j.status}</td>
            <td><a href={j.absoluteUrl} target="_blank" rel="noreferrer">vaga</a></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Opportunities() {
  const { data, error } = useAsync(api.opportunities);
  if (error) return <p className="err">{error}</p>;
  return (
    <table>
      <thead><tr><th>Status</th><th>Follow-up</th><th>Notas</th><th>Criada</th></tr></thead>
      <tbody>
        {(data ?? []).map((o: Opportunity) => (
          <tr key={o.id}>
            <td><span className="pill">{o.status}</span></td>
            <td>{o.nextFollowUpAtUtc ? new Date(o.nextFollowUpAtUtc).toLocaleDateString() : "—"}</td>
            <td>{o.notes ?? "—"}</td>
            <td>{new Date(o.createdAtUtc).toLocaleDateString()}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Digest() {
  const { data, error } = useAsync<DigestPreview>(api.digestPreview);
  if (error) return <p className="err">{error}</p>;
  if (!data) return <p>Carregando…</p>;
  return (
    <div>
      <p><strong>{data.subject}</strong> — {data.total} oportunidades
        ({data.strategicCount} estratégicas · {data.prioritizeCount} prioritárias · {data.applyCount} boas)</p>
      <div className="digest" dangerouslySetInnerHTML={{ __html: data.html }} />
    </div>
  );
}
