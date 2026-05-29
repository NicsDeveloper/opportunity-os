namespace OpportunityOS.Application.Digest;

/// <summary>A single opportunity rendered into the digest.</summary>
public sealed record OpportunityDigestItem(
    string CompanyName,
    string JobTitle,
    string JobUrl,
    int OverallScore,
    string Recommendation,
    string Rationale,
    string LinkedInMessage,
    string CoverLetter,
    string CvTailoringNotes,
    List<string> Strengths,
    List<string> Risks);

/// <summary>Rendered digest (not yet sent).</summary>
public sealed record DigestPreview(
    string Subject,
    string Markdown,
    string Html,
    int Total,
    int StrategicCount,
    int PrioritizeCount,
    int ApplyCount);

/// <summary>Outcome of an attempt to send the digest.</summary>
public sealed record DigestSendResult(
    bool Sent,
    string Reason,
    int ItemCount,
    Guid ExecutionRunId);

/// <summary>SMTP/email transport port. Implemented in Infrastructure.</summary>
public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string subject, string htmlBody, CancellationToken cancellationToken);
}

/// <summary>Data + audit persistence for the digest. Implemented in Infrastructure.</summary>
public interface IDigestStore
{
    Task<IReadOnlyList<OpportunityDigestItem>> GetDigestItemsAsync(int minScore, CancellationToken ct);
    Task SaveExecutionRunAsync(Domain.Entities.ExecutionRun run, CancellationToken ct);
}
