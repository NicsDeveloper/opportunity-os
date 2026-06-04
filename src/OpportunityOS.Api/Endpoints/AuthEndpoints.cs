using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpportunityOS.Application.Auth;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Infrastructure.Auth;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Endpoints;

/// <summary>Cookie-based auth (spec §20.1): register / login / logout / me.</summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Register: creates the user + their (single) workspace, then signs them in.
        group.MapPost("/register", async (
            RegisterRequest req, UserManager<AppUser> users, SignInManager<AppUser> signIn,
            OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Email and password are required." });

            var email = req.Email.Trim();
            if (await users.FindByEmailAsync(email) is not null)
                return Results.Conflict(new { error = "An account with this email already exists." });

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? email : req.DisplayName!.Trim(),
            };
            var created = await users.CreateAsync(user, req.Password);
            if (!created.Succeeded)
                return Results.BadRequest(new { error = "Could not create account.", details = created.Errors.Select(e => e.Description) });

            var workspace = new Workspace(user.Id, $"Workspace de {user.DisplayName}");
            db.Workspaces.Add(workspace);
            user.LastLoginAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await signIn.SignInAsync(user, isPersistent: true);
            return Results.Ok(new AuthMeResponse(user.Id, user.Email!, user.DisplayName, workspace.Id));
        }).AllowAnonymous();

        // Login: cookie sign-in via SignInManager.
        group.MapPost("/login", async (
            LoginRequest req, UserManager<AppUser> users, SignInManager<AppUser> signIn,
            OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Email and password are required." });

            var user = await users.FindByEmailAsync(req.Email.Trim());
            if (user is null) return Results.Json(new { error = "Invalid credentials." }, statusCode: StatusCodes.Status401Unauthorized);

            var result = await signIn.PasswordSignInAsync(user, req.Password, isPersistent: true, lockoutOnFailure: false);
            if (!result.Succeeded)
                return Results.Json(new { error = "Invalid credentials." }, statusCode: StatusCodes.Status401Unauthorized);

            user.LastLoginAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var workspaceId = await db.Workspaces.Where(w => w.UserId == user.Id).Select(w => (Guid?)w.Id).FirstOrDefaultAsync(ct);
            return Results.Ok(new AuthMeResponse(user.Id, user.Email!, user.DisplayName, workspaceId));
        }).AllowAnonymous();

        // Logout: clear the cookie.
        group.MapPost("/logout", async (SignInManager<AppUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        // Me: the current session's user (+ workspace), or 401.
        group.MapGet("/me", async (
            ICurrentUserContext current, UserManager<AppUser> users, OpportunityOsDbContext db, CancellationToken ct) =>
        {
            if (current.UserId is not { } userId)
                return Results.Json(new { error = "Not authenticated." }, statusCode: StatusCodes.Status401Unauthorized);

            var user = await users.FindByIdAsync(userId.ToString());
            if (user is null) return Results.Json(new { error = "Not authenticated." }, statusCode: StatusCodes.Status401Unauthorized);

            var workspaceId = await current.GetWorkspaceIdAsync(ct);
            return Results.Ok(new AuthMeResponse(user.Id, user.Email!, user.DisplayName, workspaceId));
        }).RequireAuthorization();
    }
}
