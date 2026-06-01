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
  companyWebsiteUrl?: string | null; postedAtUtc: string; rationale: string;
  discoveryRank?: number; sourceType?: string; sourceConfidenceScore?: number;
  requiresManualValidation?: boolean; realCompanyName?: string | null; sourceName?: string | null;
}
export interface Run {
  id: string; runType: string; status: string; startedAtUtc: string;
  itemsProcessed: number; itemsSucceeded: number; itemsFailed: number;
}
export interface Message {
  id: string; jobPostingId: string; emailSubject: string; status: string; createdAtUtc: string;
}
export interface GeneratedMessage {
  id: string; jobPostingId: string; opportunityMatchId: string;
  linkedInMessage: string; coverLetter: string; emailSubject: string; emailBody: string;
  cvTailoringNotes: string; followUpMessage: string; humanReviewNotes: string;
  status: string; promptVersion: string; modelName: string; createdAtUtc: string;
}
export interface Match {
  id: string; jobPostingId: string; overallScore: number; recommendation: string;
  strengths: string[]; risks: string[]; missingRequirements: string[]; rationale: string;
}
export interface AnalyzeResult { analysis: unknown; match: Match; }
export interface RawCandidate {
  id: string; title: string; snippet?: string | null; discoveredUrl: string;
  sourceProvider: string; sourceName: string; sourceType: string;
  realCompanyName?: string | null; originalJobUrl?: string | null;
  location?: string | null; discoveredAtUtc: string; publishedAtUtc?: string | null;
  status: string; sourceConfidenceScore: number; preliminaryFitScore?: number | null;
  requiresManualValidation: boolean; searchCampaignId?: string | null; query?: string | null;
}
export interface DiscoveryMetrics {
  rawCandidatesToday: number; rawCandidatesThisWeek: number; queriesToday: number;
  jobsPromotedToday: number; deduplicationRate: number; averageSourceConfidence: number;
  averageFitScore: number; actionableOpportunities: number; weakSources: number;
  relevantFeedback: number; irrelevantFeedback: number; bySourceType: Record<string, number>;
}
export interface PromotionResult { promoted: boolean; jobPostingId?: string | null; wasDuplicate: boolean; reason: string; }
export interface Profile { fullName: string; headline: string; }

export const api = {
  profile: () => get<Profile>("/candidate-profile"),
  summary: () => get<Summary>("/dashboard/summary"),
  bestOpportunities: (take = 10, minScore = 60) =>
    get<BestOpportunity[]>(`/matches?take=${take}&minScore=${minScore}`),
  // Three layers (P8): Action Today (acionável), Qualified (triado), Firehose (tudo).
  actionToday: () => get<BestOpportunity[]>(`/matches?take=10&minScore=75`),
  qualified: (take = 120) => get<BestOpportunity[]>(`/matches?take=${take}&minScore=60&sort=rank`),
  rawCandidates: (take = 150) => get<RawCandidate[]>(`/discovery/raw-candidates?take=${take}`),
  discoveryMetrics: () => get<DiscoveryMetrics>(`/discovery/metrics`),
  promoteRaw: (id: string) => post<PromotionResult>(`/discovery/raw-candidates/${id}/promote`),
  feedback: (type: string, body: { jobPostingId?: string; rawJobCandidateId?: string; reason?: string }) =>
    post(`/feedback`, { type, ...body }),
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
  analyze: (jobId: string) => post<AnalyzeResult>(`/jobs/${jobId}/ai/analyze`),
  generateOutreach: (jobId: string) => post<GeneratedMessage>(`/jobs/${jobId}/ai/generate-outreach`),
  backfillWebsites: () => post<{ processed: number; found: number }>("/companies/backfill-websites"),
};
