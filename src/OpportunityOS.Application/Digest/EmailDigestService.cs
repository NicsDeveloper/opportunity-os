using Microsoft.Extensions.Logging;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Application.Digest;

public interface IEmailDigestService
{
    /// <summary>Render (without sending) the digest for a specific profile.</summary>
    Task<DigestPreview> BuildPreviewAsync(Guid candidateProfileId, int minScore, CancellationToken ct);
    /// <summary>Build and send the digest for a specific profile.</summary>
    Task<DigestSendResult> SendDailyDigestAsync(Guid candidateProfileId, int minScore, CancellationToken ct);
    /// <summary>Send a digest for every profile (using each profile's MinimumScoreToShow).</summary>
    Task<IReadOnlyList<DigestSendResult>> SendAllAsync(CancellationToken ct);
}

/// <summary>
/// Builds and (on explicit request) sends the opportunities digest. Sending is
/// never automatic at construction time; it only happens when this method is
/// invoked by the endpoint or the daily job. No attachments, no applications.
/// </summary>
public sealed class EmailDigestService : IEmailDigestService
{
    public const int DefaultMinScore = 60;

    private readonly IDigestStore _store;
    private readonly IEmailSender _sender;
    private readonly ILogger<EmailDigestService> _logger;

    public EmailDigestService(IDigestStore store, IEmailSender sender, ILogger<EmailDigestService> logger)
    {
        _store = store;
        _sender = sender;
        _logger = logger;
    }

    public async Task<DigestPreview> BuildPreviewAsync(Guid candidateProfileId, int minScore, CancellationToken ct)
    {
        var items = await _store.GetDigestItemsAsync(candidateProfileId, minScore, ct);
        var name = await _store.GetCandidateNameAsync(candidateProfileId, ct);
        return DigestRenderer.Render(items, name);
    }

    public async Task<IReadOnlyList<DigestSendResult>> SendAllAsync(CancellationToken ct)
    {
        var profiles = await _store.GetProfilesAsync(ct);
        var results = new List<DigestSendResult>(profiles.Count);
        foreach (var p in profiles)
            results.Add(await SendDailyDigestAsync(p.Id, p.MinimumScoreToShow, ct));
        return results;
    }

    public async Task<DigestSendResult> SendDailyDigestAsync(Guid candidateProfileId, int minScore, CancellationToken ct)
    {
        var run = ExecutionRun.Start("SendDailyDigest");
        var items = await _store.GetDigestItemsAsync(candidateProfileId, minScore, ct);
        var candidateName = await _store.GetCandidateNameAsync(candidateProfileId, ct);

        // No relevant opportunities: record the run without error and don't send.
        if (items.Count == 0)
        {
            run.Complete();
            await _store.SaveExecutionRunAsync(run, ct);
            _logger.LogInformation("DigestSkipped: no relevant opportunities");
            return new DigestSendResult(false, "No relevant opportunities; nothing sent.", 0, run.Id);
        }

        if (!_sender.IsConfigured)
        {
            run.RecordFailure("SMTP not configured");
            run.Complete();
            await _store.SaveExecutionRunAsync(run, ct);
            return new DigestSendResult(false, "SMTP not configured; use the preview or set Email:Smtp:*.", items.Count, run.Id);
        }

        var preview = DigestRenderer.Render(items, candidateName);
        try
        {
            await _sender.SendAsync(preview.Subject, preview.Html, ct);
            run.RecordSuccess(items.Count);
            run.Complete();
            await _store.SaveExecutionRunAsync(run, ct);
            _logger.LogInformation("DigestSent: {Count} opportunities", items.Count);
            return new DigestSendResult(true, "Digest sent.", items.Count, run.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailFailed sending digest");
            run.RecordFailure(ex.Message);
            run.Complete();
            await _store.SaveExecutionRunAsync(run, ct);
            return new DigestSendResult(false, $"Send failed: {ex.Message}", items.Count, run.Id);
        }
    }
}
