// Thin API client. All calls go through the Vite dev proxy (/api -> backend).

// credentials:"include" sends the auth cookie on every call (through the Vite proxy).
async function get<T>(path: string): Promise<T> {
  const res = await fetch(`/api${path}`, { headers: { Accept: "application/json" }, credentials: "include" });
  if (!res.ok) throw new ApiError(res.status, `${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}
async function post<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`/api${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) throw new ApiError(res.status, await errorText(res));
  return (res.status === 204 ? (undefined as T) : (res.json() as Promise<T>));
}
async function put<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`/api${path}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) throw new ApiError(res.status, await errorText(res));
  return (res.status === 204 ? (undefined as T) : (res.json() as Promise<T>));
}
async function del<T>(path: string): Promise<T> {
  const res = await fetch(`/api${path}`, { method: "DELETE", credentials: "include" });
  if (!res.ok) throw new ApiError(res.status, `${res.status} ${res.statusText}`);
  return (res.status === 204 ? (undefined as T) : (res.json() as Promise<T>));
}

async function postForm<T>(path: string, form: FormData): Promise<T> {
  const res = await fetch(`/api${path}`, { method: "POST", credentials: "include", body: form });
  if (!res.ok) throw new ApiError(res.status, await errorText(res));
  return (res.status === 204 ? (undefined as T) : (res.json() as Promise<T>));
}

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}
async function errorText(res: Response): Promise<string> {
  try {
    const body = await res.json() as { error?: string; details?: string[] };
    return body?.error ?? (body?.details?.join("; ")) ?? `${res.status} ${res.statusText}`;
  } catch { return `${res.status} ${res.statusText}`; }
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
  datePrecise?: boolean;
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
export interface BacenPreview {
  companies: number; strategic: number; high: number; medium: number; low: number;
  estimatedQueries: number; estimatedBudgetCost: number;
}
export interface ConsultingCandidate {
  id: string; name: string; websiteUrl?: string | null; country: string; source: string;
  signals: string[]; consultingConfidenceScore: number; status: string; createdAtUtc: string;
}
export interface Campaign {
  id: string; name: string; description: string; status: string; priority: string;
  baseKeywords: string[]; dailyQueryBudget: number; lastRunAtUtc?: string | null;
}
export interface FirehoseRun {
  executionRunId: string; status: string; queriesExecuted: number; resultsCount: number;
  newCandidates: number; duplicates: number; errors: number;
}
export interface Application {
  jobPostingId: string; jobTitle: string; companyName: string; jobUrl: string;
  companyWebsiteUrl?: string | null; overallScore: number; action: string;
  appliedAtUtc: string; postedAtUtc: string;
}
export interface DiscoveryResult {
  executionRunId: string; status: string; companiesProcessed: number;
  providersInvoked: number; jobsDiscovered: number; jobsUpdated: number; errors: number;
}
export interface Profile {
  id: string; fullName: string; displayName: string; headline: string; summary: string;
  location: string; seniority: string; preferredLanguage: string; isDefault: boolean;
  coreSkills: string[]; secondarySkills: string[]; excludedStacks: string[]; domains: string[];
  preferredRoles: string[]; preferredContractTypes: string[]; preferredLocations: string[];
  preferredWorkModes: string[]; minimumScoreToShow: number;
}
// Payload for create/update. List fields optional; backend fills sane defaults.
export interface ProfileInput {
  fullName: string; displayName?: string; headline: string; summary: string; location: string;
  seniority: string; preferredLanguage: string;
  coreSkills?: string[]; secondarySkills?: string[]; excludedStacks?: string[]; domains?: string[];
  preferredRoles?: string[]; preferredContractTypes?: string[]; preferredLocations?: string[];
  preferredWorkModes?: string[]; minimumScoreToShow?: number;
}

// ---- Auth & Workspace ----
export interface AuthMe { id: string; email: string; displayName: string; workspaceId: string | null; isAdmin: boolean; }
export interface AdminRun {
  id: string; runType: string; status: string; startedAtUtc: string; finishedAtUtc: string | null;
  itemsProcessed: number; itemsSucceeded: number; itemsFailed: number;
}
export interface AdminOverview {
  companies: number; scannable: number; jobs: number; matches: number;
  runs: AdminRun[]; recurring: { name: string; cron: string; desc: string }[];
}
export interface WorkspaceMe {
  workspaceId: string; name: string;
  user: { id: string; email: string; displayName: string };
  defaultCandidateProfileId: string | null;
}

// ---- LinkedIn PDF import ----
export interface LinkedInExperience {
  company: string; title: string; location?: string | null;
  startDateText?: string | null; endDateText?: string | null; durationText?: string | null; description?: string | null;
}
export interface LinkedInParsedProfile {
  fullName?: string | null; headline?: string | null; location?: string | null; email?: string | null;
  linkedInUrl?: string | null; summary?: string | null;
  skills: string[]; certifications: string[]; experiences: LinkedInExperience[];
  education: { institution: string; degree?: string | null; field?: string | null; periodText?: string | null }[];
  confidence: { score: number; signals: string[]; missingSignals: string[] };
}
export interface CandidateProfileDraft {
  displayName: string; fullName: string; headline: string; summary: string; location: string;
  seniority: string; preferredLanguage: string;
  coreSkills: string[]; secondarySkills: string[]; excludedStacks: string[]; domains: string[];
  preferredRoles: string[]; preferredContractTypes: string[]; preferredLocations: string[];
  preferredWorkModes: string[]; minimumScoreToShow: number;
  experiences: { company: string; role: string; period: string; technologies: string[]; achievements: string[] }[];
}
export interface UploadLinkedInPdfResponse {
  importId: string; parsedProfile: LinkedInParsedProfile; draft: CandidateProfileDraft; warnings: string[];
}

// Append &candidateProfileId=… (or ?… when first param) when a profile is selected.
function pid(profileId?: string, first = false) {
  if (!profileId) return "";
  return `${first ? "?" : "&"}candidateProfileId=${profileId}`;
}

export const api = {
  // Auth: me() resolves null on 401 so the UI can render the login screen instead of throwing.
  auth: {
    me: async (): Promise<AuthMe | null> => {
      try { return await get<AuthMe>("/auth/me"); }
      catch (e) { if (e instanceof ApiError && e.status === 401) return null; throw e; }
    },
    register: (email: string, password: string, displayName?: string) =>
      post<AuthMe>("/auth/register", { email, password, displayName }),
    login: (email: string, password: string) =>
      post<AuthMe>("/auth/login", { email, password }),
    logout: () => post<void>("/auth/logout"),
  },
  workspaceMe: () => get<WorkspaceMe>("/workspace/me"),
  profileImports: {
    uploadLinkedInPdf: (file: File) => {
      const form = new FormData();
      form.append("file", file, file.name);
      return postForm<UploadLinkedInPdfResponse>("/profile-imports/linkedin-pdf", form);
    },
    apply: (importId: string, draft: CandidateProfileDraft, setAsDefault: boolean) =>
      post<Profile>(`/profile-imports/${importId}/apply`, { draft, setAsDefault }),
  },
  admin: {
    overview: () => get<AdminOverview>("/admin/overview"),
    sweep: (maxCompanies?: number, maxDurationSeconds?: number) =>
      post<{ started: boolean; maxCompanies: number | null; maxDurationSeconds: number }>(
        "/admin/sweep", { maxCompanies, maxDurationSeconds }),
  },
  profile: () => get<Profile>("/candidate-profile"),
  // Multi-profile: list, create, edit, and move the default anchor.
  profiles: () => get<Profile[]>("/candidate-profiles"),
  createProfile: (body: ProfileInput) => post<Profile>("/candidate-profiles", body),
  updateProfile: (id: string, body: ProfileInput) => put<Profile>(`/candidate-profiles/${id}`, body),
  setDefaultProfile: (id: string) => post<Profile>(`/candidate-profiles/${id}/set-default`, {}),
  summary: (profileId?: string) => get<Summary>(`/dashboard/summary${pid(profileId)}`),
  bestOpportunities: (take = 10, minScore = 60, profileId?: string) =>
    get<BestOpportunity[]>(`/matches?take=${take}&minScore=${minScore}${pid(profileId)}`),
  // Three layers (P8): Action Today (acionável), Qualified (triado), Firehose (tudo).
  actionToday: (region = "all", contract = "all", profileId?: string) =>
    get<BestOpportunity[]>(`/matches?take=10&minScore=75&region=${region}&contract=${contract}${pid(profileId)}`),
  // Main board: order by fit (overallScore + freshness) so the highest-adherence roles are
  // inside the returned window — the UI then sorts by score. (sort=rank buried high-fit jobs
  // from weaker sources outside the window.)
  qualified: (take = 500, region = "all", contract = "all", profileId?: string) =>
    get<BestOpportunity[]>(`/matches?take=${take}&minScore=60&region=${region}&contract=${contract}${pid(profileId)}`),
  allOpportunities: (take = 80, region = "all", contract = "all", profileId?: string) =>
    get<BestOpportunity[]>(`/matches?take=${take}&minScore=45&sort=rank&region=${region}&contract=${contract}${pid(profileId)}`),
  rawCandidates: (take = 150) => get<RawCandidate[]>(`/discovery/raw-candidates?take=${take}`),
  discoveryMetrics: () => get<DiscoveryMetrics>(`/discovery/metrics`),
  promoteRaw: (id: string) => post<PromotionResult>(`/discovery/raw-candidates/${id}/promote`),
  resolveOriginal: (id: string) =>
    post<{ found: boolean; originalUrl?: string | null; companyName?: string | null; atsProvider?: string | null; confidence: number; reason?: string | null }>(`/discovery/raw-candidates/${id}/resolve-original`),
  feedback: (type: string, body: { jobPostingId?: string; rawJobCandidateId?: string; reason?: string; candidateProfileId?: string }) =>
    post(`/feedback`, { type, ...body }),
  // Applications board: opportunities already acted on ("já me cadastrei"), per profile.
  applications: (profileId?: string) => get<Application[]>(`/applications${pid(profileId, true)}`),
  llmRerank: (take = 10, profileId?: string) =>
    post<{ rescored: number; changed: number; skippedByBudget: number; failed: number; llmConfigured: boolean }>(
      `/matches/llm-rerank?take=${take}${pid(profileId)}`),
  unapply: (jobId: string, profileId?: string) => del<void>(`/applications/${jobId}${pid(profileId, true)}`),
  // Descobrir mais (B3): bancos/fintechs, consultorias, buscas salvas.
  bacenPreview: (minimumPriority = "High") =>
    get<BacenPreview>(`/discovery/bacen-financial-sweep/preview?minimumPriority=${minimumPriority}&maxCompanies=100&maxQueriesPerCompany=8`),
  runBacenSweep: () =>
    post<FirehoseRun>(`/discovery/bacen-financial-sweep`, { minimumPriority: "High", maxCompanies: 60, maxQueriesPerCompany: 8, saveRawCandidates: true }),
  consultingCandidates: (take = 60) => get<ConsultingCandidate[]>(`/consulting-radar/candidates?take=${take}`),
  runConsultingDiscover: () => post<{ candidatesFound: number; status: string }>(`/consulting-radar/discover`, { includeSeeds: true }),
  promoteConsulting: (id: string) => post<{ considered: number; promoted: number; skipped: number }>(`/consulting-radar/candidates/${id}/promote-to-company`),
  campaigns: () => get<Campaign[]>(`/discovery/campaigns`),
  runCampaign: (id: string) => post<FirehoseRun>(`/discovery/campaigns/${id}/run`, {}),
  companies: () => get<Company[]>("/companies"),
  jobs: () => get<Job[]>("/jobs"),
  opportunities: () => get<Opportunity[]>("/opportunities"),
  followUps: () => get<Opportunity[]>("/opportunities/follow-ups"),
  runs: (take = 8) => get<Run[]>(`/runs?take=${take}`),
  messages: () => get<Message[]>("/messages"),
  digestPreview: () => get<DigestPreview>("/digest/preview"),
  search: (keywords: string[]) =>
    post<DiscoveryResult>("/jobs/search", { keywords }),
  discover: (companyId?: string) => post("/jobs/discover", { companyId: companyId ?? null }),
  sendDigest: () => post<{ sent: boolean; itemCount: number; reason: string }>("/digest/send"),
  analyze: (jobId: string, profileId?: string) => post<AnalyzeResult>(`/jobs/${jobId}/ai/analyze${pid(profileId, true)}`),
  generateOutreach: (jobId: string, profileId?: string) => post<GeneratedMessage>(`/jobs/${jobId}/ai/generate-outreach${pid(profileId, true)}`),
  backfillWebsites: () => post<{ processed: number; found: number }>("/companies/backfill-websites"),
};
