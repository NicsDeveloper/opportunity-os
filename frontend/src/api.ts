// Thin API client. All calls go through the Vite dev proxy (/api -> backend).

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`/api${path}`, { headers: { Accept: "application/json" } });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}

async function post<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`/api${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}

export interface Company {
  id: string; name: string; priority: string; source: string;
  careersUrl?: string | null; websiteUrl?: string | null;
  tags: string[]; lastScannedAtUtc?: string | null;
}
export interface Job {
  id: string; title: string; sourceProvider: string; location?: string | null;
  status: string; absoluteUrl: string;
}
export interface Opportunity {
  id: string; jobPostingId: string; status: string;
  nextFollowUpAtUtc?: string | null; notes?: string | null; createdAtUtc: string;
}
export interface DigestPreview {
  subject: string; html: string; total: number;
  strategicCount: number; prioritizeCount: number; applyCount: number;
}

export const api = {
  companies: () => get<Company[]>("/companies"),
  jobs: () => get<Job[]>("/jobs"),
  opportunities: () => get<Opportunity[]>("/opportunities"),
  digestPreview: () => get<DigestPreview>("/digest/preview"),
  discover: () => post("/jobs/discover", {}),
};
