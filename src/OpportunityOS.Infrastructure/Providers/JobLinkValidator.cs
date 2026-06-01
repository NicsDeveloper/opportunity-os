using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Infrastructure.Providers;

/// <summary>
/// HEAD-checks the oldest-touched active postings; marks 404/410 as expired so the fresh
/// feed stops surfacing dead links. Network hiccups are ignored (posting left as-is).
/// </summary>
public sealed class JobLinkValidator : IJobLinkValidator
{
    private readonly OpportunityOsDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<JobLinkValidator> _logger;

    public JobLinkValidator(OpportunityOsDbContext db, HttpClient http, ILogger<JobLinkValidator> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task<LinkValidationResult> ValidateAsync(int max, CancellationToken ct)
    {
        var limit = Math.Clamp(max, 1, 500);
        var jobs = await _db.JobPostings
            .Where(j => j.Status != JobPostingStatus.Expired && j.Status != JobPostingStatus.Archived)
            .OrderBy(j => j.UpdatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        int checkd = 0, expired = 0;
        foreach (var job in jobs)
        {
            checkd++;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, job.AbsoluteUrl);
                using var resp = await _http.SendAsync(req, ct);
                if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    job.MarkExpired();
                    expired++;
                }
            }
            catch { /* network hiccup: leave as-is */ }
        }
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Link validation: checked {Checked}, expired {Expired}", checkd, expired);
        return new LinkValidationResult(checkd, expired);
    }
}
