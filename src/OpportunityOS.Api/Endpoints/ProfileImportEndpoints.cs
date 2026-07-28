using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Application.Import;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>
/// LinkedIn PDF profile import (optional onboarding accelerator). Workspace-scoped and auth-only.
/// Upload → extract → detect → parse → map → (optional LLM normalize) → editable draft; apply creates
/// the real CandidateProfile from the REVIEWED draft. Only the parsed JSON is persisted (privacy).
/// </summary>
public static class ProfileImportEndpoints
{
    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapProfileImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile-imports").WithTags("ProfileImport").RequireAuthorization();

        group.MapPost("/linkedin-pdf", async (
            IFormFile? file, ICurrentUserContext user, OpportunityOsDbContext db,
            IPdfTextExtractor extractor, ILinkedInPdfProfileDetector detector, ILinkedInProfilePdfParser parser,
            ILinkedInProfileToCandidateProfileDraftMapper mapper, IProfileImportNormalizer normalizer,
            CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            if (ws is null) return Results.BadRequest(new { error = "No workspace for this user." });

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Envie um arquivo PDF." });
            if (file.Length > MaxBytes)
                return Results.BadRequest(new { error = "Arquivo muito grande (máx. 5 MB)." });
            var isPdfExt = file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var okType = file.ContentType is "application/pdf" or "application/octet-stream" or "" || file.ContentType is null;
            if (!isPdfExt || !okType)
                return Results.BadRequest(new { error = "Formato inválido. Envie o PDF exportado do LinkedIn." });

            string text;
            try
            {
                await using var stream = file.OpenReadStream();
                text = await extractor.ExtractTextAsync(stream, ct);
            }
            catch (PdfTextExtractionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            var detection = detector.Detect(text);
            if (!detection.IsLikelyLinkedInProfilePdf)
                return Results.BadRequest(new { error = "Este PDF não parece ter sido exportado do LinkedIn." });

            var parsed = parser.Parse(text, detection, out var warnings);
            var draft = mapper.Map(parsed);
            draft = await normalizer.NormalizeAsync(parsed, draft, ct);

            var import = new ProfileImport(ws.Value, ProfileImportSource.LinkedInPdf,
                file.FileName, file.ContentType ?? "application/pdf", file.Length);
            import.MarkParsed(JsonSerializer.Serialize(parsed, Json)); // only the parsed JSON is persisted
            db.ProfileImports.Add(import);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new UploadLinkedInProfilePdfResponse(import.Id, parsed, draft, warnings));
        }).DisableAntiforgery();

        group.MapGet("/{id:guid}", async (Guid id, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            var import = await db.ProfileImports.FirstOrDefaultAsync(i => i.Id == id && i.WorkspaceId == ws, ct);
            if (import is null) return Results.NotFound();
            return Results.Ok(ToResponse(import));
        });

        group.MapPost("/{id:guid}/apply", async (
            Guid id, ApplyProfileImportRequest req, ICurrentUserContext user, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            var ws = await user.GetWorkspaceIdAsync(ct);
            if (ws is null) return Results.BadRequest(new { error = "No workspace for this user." });

            var import = await db.ProfileImports.FirstOrDefaultAsync(i => i.Id == id && i.WorkspaceId == ws, ct);
            if (import is null) return Results.NotFound();

            // Idempotent / double-click-safe: already applied → return the existing profile.
            if (import.Status == ProfileImportStatus.Applied && import.CandidateProfileId is { } existingId)
            {
                var existing = await db.CandidateProfiles.FirstOrDefaultAsync(p => p.Id == existingId && p.WorkspaceId == ws, ct);
                if (existing is not null) return Results.Ok(existing.ToResponse());
            }

            var d = req.Draft;
            var experiences = (d.Experiences ?? new()).Select(e => new CandidateExperience
            {
                Company = e.Company, Role = e.Role, Period = e.Period,
                Technologies = e.Technologies ?? new(), Achievements = e.Achievements ?? new()
            });

            var profile = new CandidateProfile(
                d.FullName, d.Headline, d.Summary, d.Location, d.Seniority, d.PreferredLanguage,
                d.CoreSkills, d.SecondarySkills, d.Domains, d.PreferredRoles, d.PreferredContractTypes,
                d.PreferredLocations, experiences, d.DisplayName, d.ExcludedStacks, d.PreferredWorkModes,
                d.MinimumScoreToShow);
            profile.AssignWorkspace(ws.Value);

            var isFirst = !await db.CandidateProfiles.AnyAsync(p => p.WorkspaceId == ws, ct);
            if (isFirst || req.SetAsDefault)
            {
                foreach (var other in await db.CandidateProfiles.Where(p => p.WorkspaceId == ws && p.IsDefault).ToListAsync(ct))
                    other.SetDefault(false);
                profile.SetDefault(true);
            }

            db.CandidateProfiles.Add(profile);
            import.MarkApplied(profile.Id);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/candidate-profiles/{profile.Id}", profile.ToResponse());
        });
    }

    private static ProfileImportResponse ToResponse(ProfileImport i)
    {
        LinkedInProfileImportDto? parsed = null;
        if (!string.IsNullOrWhiteSpace(i.ParsedJson))
            try { parsed = JsonSerializer.Deserialize<LinkedInProfileImportDto>(i.ParsedJson, Json); } catch { /* ignore */ }
        return new ProfileImportResponse(i.Id, i.Status.ToString(), i.Source.ToString(), i.CandidateProfileId, parsed, i.CreatedAtUtc);
    }
}
