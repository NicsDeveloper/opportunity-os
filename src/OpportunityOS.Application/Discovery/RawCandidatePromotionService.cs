using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Application.Discovery;

public interface IRawCandidatePromotionService
{
    Task<PromotionResultResponse> PromoteAsync(Guid rawCandidateId, CancellationToken ct);
    Task<BatchPromotionResponse> PromoteBatchAsync(PromoteBatchRequest req, CancellationToken ct);
}

/// <summary>
/// Promotes a RawJobCandidate (Firehose) into a qualified JobPosting (P0 bridge). Dedups by
/// fingerprint: a vacancy already present becomes a JobPostingSourceOccurrence on the existing
/// posting (best source kept as primary). New postings are scored (heuristic) so they reach the
/// Qualified/Action feeds. Aggregator candidates without a confirmed company are NOT promoted.
/// </summary>
public sealed class RawCandidatePromotionService : IRawCandidatePromotionService
{
    private readonly IFirehoseStore _firehose;
    private readonly IDiscoveryStore _discovery;
    private readonly IJobFingerprintService _fingerprint;
    private readonly IJobNormalizer _normalizer;
    private readonly IMatchEngine _matchEngine;
    private readonly ILogger<RawCandidatePromotionService> _logger;

    public RawCandidatePromotionService(
        IFirehoseStore firehose, IDiscoveryStore discovery, IJobFingerprintService fingerprint,
        IJobNormalizer normalizer, IMatchEngine matchEngine, ILogger<RawCandidatePromotionService> logger)
    {
        _firehose = firehose;
        _discovery = discovery;
        _fingerprint = fingerprint;
        _normalizer = normalizer;
        _matchEngine = matchEngine;
        _logger = logger;
    }

    public async Task<PromotionResultResponse> PromoteAsync(Guid rawCandidateId, CancellationToken ct)
    {
        var candidate = await _firehose.GetRawCandidateAsync(rawCandidateId, ct);
        if (candidate is null) return new PromotionResultResponse(false, null, false, "Candidato não encontrado");

        var profile = await _discovery.GetActiveProfileAsync(ct);
        var result = await PromoteCoreAsync(candidate, profile, ct);
        await _firehose.SaveChangesAsync(ct);
        return result;
    }

    public async Task<BatchPromotionResponse> PromoteBatchAsync(PromoteBatchRequest req, CancellationToken ct)
    {
        var take = Math.Clamp(req.MaxCandidates ?? 50, 1, 500);
        var minConf = req.MinSourceConfidence ?? 50;
        var requireCompany = req.RequireRealCompany ?? true;

        var candidates = await _firehose.GetPromotableAsync(minConf, requireCompany, take, ct);
        var profile = await _discovery.GetActiveProfileAsync(ct);

        int promoted = 0, duplicates = 0, skipped = 0;
        foreach (var c in candidates)
        {
            var r = await PromoteCoreAsync(c, profile, ct);
            if (!r.Promoted) skipped++;
            else if (r.WasDuplicate) duplicates++;
            else promoted++;
            await _firehose.SaveChangesAsync(ct);
        }
        _logger.LogInformation("PromoteBatch: considered={C} promoted={P} duplicates={D} skipped={S}",
            candidates.Count, promoted, duplicates, skipped);
        return new BatchPromotionResponse(candidates.Count, promoted, duplicates, skipped);
    }

    private async Task<PromotionResultResponse> PromoteCoreAsync(RawJobCandidate candidate, CandidateProfile? profile, CancellationToken ct)
    {
        if (candidate.Status == RawJobCandidateStatus.PromotedToJobPosting && candidate.PromotedJobPostingId is { } already)
            return new PromotionResultResponse(true, already, false, "Já promovido");

        // Aggregator/social without a confirmed real company is not promoted to a Company.
        var companyName = candidate.RealCompanyName;
        if (string.IsNullOrWhiteSpace(companyName))
        {
            candidate.SetStatus(RawJobCandidateStatus.Rejected);
            return new PromotionResultResponse(false, null, false, "Empresa não confirmada — não promovido");
        }

        var fingerprint = candidate.NormalizedFingerprint
            ?? _fingerprint.GenerateFingerprint(new JobFingerprintInput(candidate.Title, companyName, candidate.Location, null));

        // Dedup: same vacancy already a JobPosting -> register a source occurrence, keep best as primary.
        var existing = await _firehose.FindJobByFingerprintAsync(fingerprint, ct);
        if (existing is not null)
        {
            await AddOccurrenceAsync(existing, candidate, ct);
            if (candidate.SourceConfidenceScore > existing.SourceConfidenceScore)
                existing.SetSourceQuality(candidate.SourceType, candidate.SourceName, candidate.SourceConfidenceScore,
                    candidate.RequiresManualValidation, candidate.RealCompanyName, candidate.OriginalJobUrl ?? candidate.DiscoveredUrl);
            candidate.MarkPromoted(existing.Id);
            return new PromotionResultResponse(true, existing.Id, true, "Duplicado — ocorrência registrada na vaga existente");
        }

        var company = await _discovery.FindOrCreateCompanyByNameAsync(companyName, ct);
        // If the source is the company's own site/career page, store its domain so the
        // frontend can fetch the real logo (Clearbit/favicon).
        if (string.IsNullOrWhiteSpace(company.WebsiteUrl)
            && candidate.SourceType is SourceType.OfficialCareerPage or SourceType.SearchResult)
        {
            var domain = DomainOf(candidate.DiscoveredUrl);
            if (domain is not null) company.SetWebsiteUrl($"https://{domain}");
        }
        var url = candidate.OriginalJobUrl ?? candidate.DiscoveredUrl;
        var job = new JobPosting(
            company.Id, externalId: fingerprint, sourceProvider: candidate.SourceProvider,
            title: candidate.Title, absoluteUrl: url, descriptionText: candidate.Snippet ?? string.Empty,
            location: candidate.Location, language: candidate.Language, publishedAtUtc: candidate.PublishedAtUtc);
        job.SetFingerprint(fingerprint);
        job.SetSourceQuality(candidate.SourceType, candidate.SourceName, candidate.SourceConfidenceScore,
            candidate.RequiresManualValidation, candidate.RealCompanyName, candidate.OriginalJobUrl);
        await _discovery.AddJobAsync(job, ct);
        await AddOccurrenceAsync(job, candidate, ct);

        // Heuristic score so it surfaces on the Qualified/Action feeds (no LLM here).
        if (profile is not null)
        {
            try
            {
                var norm = _normalizer.Normalize(job);
                job.ApplyNormalization(norm.Seniority, norm.WorkMode, norm.Language, norm.Skills, norm.Domains);
                var r = _matchEngine.Evaluate(profile, job);
                job.MarkAnalyzed();
                await _discovery.AddMatchAsync(new OpportunityMatch(
                    job.Id, profile.Id, r.OverallScore, r.TechnicalScore, r.DomainScore, r.SeniorityScore,
                    r.LocationScore, r.LanguageScore, r.Recommendation, r.Strengths, r.Risks,
                    r.MissingRequirements, r.Rationale, HeuristicMatchEngine.Version), ct);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Promotion scoring failed for {JobId}", job.Id); }
        }

        candidate.MarkPromoted(job.Id);
        return new PromotionResultResponse(true, job.Id, false, "Promovido para vaga");
    }

    private static string? DomainOf(string url)
    {
        try
        {
            var host = new Uri(url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
            // Skip multi-tenant platforms (their host isn't the company's own domain).
            string[] platforms = { "gupy.io", "greenhouse.io", "lever.co", "ashbyhq.com", "smartrecruiters.com", "workdayjobs.com", "linkedin.", "indeed.", "glassdoor." };
            return platforms.Any(p => host.Contains(p, StringComparison.OrdinalIgnoreCase)) ? null : host;
        }
        catch { return null; }
    }

    private Task AddOccurrenceAsync(JobPosting job, RawJobCandidate candidate, CancellationToken ct) =>
        _firehose.AddSourceOccurrenceAsync(new JobPostingSourceOccurrence(
            job.Id, candidate.SourceProvider, candidate.SourceName, candidate.SourceType,
            candidate.DiscoveredUrl, candidate.SourceConfidenceScore), ct);
}
