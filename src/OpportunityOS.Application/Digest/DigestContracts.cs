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
    List<string> Risks,
    string? OutreachLanguage = null);

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

/// <summary>A profile the digest can be built/sent for.</summary>
public sealed record DigestProfile(Guid Id, string Name, int MinimumScoreToShow);

/// <summary>Data + audit persistence for the digest. Implemented in Infrastructure.</summary>
public interface IDigestStore
{
    /// <summary>Profiles to build/send a digest for (default first).</summary>
    Task<IReadOnlyList<DigestProfile>> GetProfilesAsync(CancellationToken ct);
    Task<IReadOnlyList<OpportunityDigestItem>> GetDigestItemsAsync(Guid candidateProfileId, int minScore, CancellationToken ct);
    /// <summary>The profile's candidate name (for greeting the recipient), or null.</summary>
    Task<string?> GetCandidateNameAsync(Guid candidateProfileId, CancellationToken ct);
    Task SaveExecutionRunAsync(Domain.Entities.ExecutionRun run, CancellationToken ct);
}
