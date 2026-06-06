using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Domain.Entities;

/// <summary>
/// An onboarding import of a professional profile (currently the LinkedIn-exported PDF).
/// Belongs to a workspace; isolated like every other personal entity. For privacy we persist
/// ONLY the parsed JSON (the raw extracted PDF text is processed in memory and discarded).
/// </summary>
public sealed class ProfileImport
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    /// <summary>Set once the draft is applied and a real profile is created.</summary>
    public Guid? CandidateProfileId { get; private set; }

    public ProfileImportSource Source { get; private set; }
    public ProfileImportStatus Status { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }

    /// <summary>Structured parse result (the LinkedInProfileImportDto serialized). The only thing we keep.</summary>
    public string ParsedJson { get; private set; } = string.Empty;
    public string? ErrorMessage { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ParsedAtUtc { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }

    private ProfileImport() { }

    public ProfileImport(Guid workspaceId, ProfileImportSource source, string originalFileName, string contentType, long fileSizeBytes)
    {
        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        Source = source;
        Status = ProfileImportStatus.Uploaded;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkParsed(string parsedJson)
    {
        ParsedJson = parsedJson;
        Status = ProfileImportStatus.Parsed;
        ParsedAtUtc = DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = ProfileImportStatus.Failed;
        ErrorMessage = errorMessage;
    }

    public void MarkApplied(Guid candidateProfileId)
    {
        CandidateProfileId = candidateProfileId;
        Status = ProfileImportStatus.Applied;
        AppliedAtUtc = DateTime.UtcNow;
    }
}
