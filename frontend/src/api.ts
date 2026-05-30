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
  return (res.status === 204 ? (undefined as T) : (res.json() as Promise<T>));
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
export interface Summary {
  jobsDiscovered: number; jobsToday: number;
  matchesAbove75: number; matchesAbove75Today: number;
  messagesGenerated: number; messagesToday: number;
  emailsSent: number; emailsToday: number;
  followUpsPending: number; nextFollowUpInDays: number | null;
}
export interface BestOpportunity {
  matchId: string; jobPostingId: string; jobTitle: string; companyName: string;
  skills: string[]; overallScore: number; recommendation: string; jobUrl: string;
  companyWebsiteUrl?: string | null;
}
export interface Run {
  id: string; runType: string; status: string; startedAtUtc: string;
  itemsProcessed: number; itemsSucceeded: number; itemsFailed: number;
}
export interface Message {
  id: string; jobPostingId: string; emailSubject: string; status: string; createdAtUtc: string;
}
export interface Profile { fullName: string; headline: string; }

export const api = {
  profile: () => get<Profile>("/candidate-profile"),
  summary: () => get<Summary>("/dashboard/summary"),
  bestOpportunities: (take = 10, minScore = 0) =>
    get<BestOpportunity[]>(`/matches?take=${take}&minScore=${minScore}`),
  companies: () => get<Company[]>("/companies"),
  jobs: () => get<Job[]>("/jobs"),
  opportunities: () => get<Opportunity[]>("/opportunities"),
  followUps: () => get<Opportunity[]>("/opportunities/follow-ups"),
  runs: (take = 8) => get<Run[]>(`/runs?take=${take}`),
  messages: () => get<Message[]>("/messages"),
  digestPreview: () => get<DigestPreview>("/digest/preview"),
  search: (keywords: string[]) => post("/jobs/search", { keywords }),
  discover: (companyId?: string) => post("/jobs/discover", { companyId: companyId ?? null }),
  sendDigest: () => post<{ sent: boolean; itemCount: number; reason: string }>("/digest/send"),
  generateOutreach: (jobId: string) => post(`/jobs/${jobId}/ai/generate-outreach`),
  backfillWebsites: () => post<{ processed: number; found: number }>("/companies/backfill-websites"),
};
