using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Contracts;
using OpportunityOS.Infrastructure.Auth;
using OpportunityOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace OpportunityOS.Api.Endpoints;

/// <summary>Workspace bootstrap for the frontend (spec §20.2).</summary>
public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspace").WithTags("Workspace").RequireAuthorization();

        group.MapGet("/me", async (
            ICurrentUserContext current, UserManager<AppUser> users,
            OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (current.UserId is not { } userId)
                return Results.Json(new { error = "Not authenticated." }, statusCode: StatusCodes.Status401Unauthorized);

            var workspace = await db.Workspaces.FirstOrDefaultAsync(w => w.UserId == userId, ct);
            if (workspace is null) return Results.NotFound(new { error = "No workspace for this user." });

            var user = await users.FindByIdAsync(userId.ToString());
            var defaultProfileId = await db.CandidateProfiles
                .Where(p => p.WorkspaceId == workspace.Id)
                .OrderByDescending(p => p.IsDefault)
                .ThenByDescending(p => p.CreatedAtUtc)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new WorkspaceMeResponse(
                workspace.Id, workspace.Name,
                new AuthUserResponse(userId, user?.Email ?? "", user?.DisplayName ?? ""),
                defaultProfileId));
        });
    }
}
