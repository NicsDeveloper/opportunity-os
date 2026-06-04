using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Infrastructure.Auth;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.Api.Auth;

/// <summary>
/// Dev-only convenience: provisions a local user (<c>dev@local</c>) owning a workspace, and backfills
/// every pre-auth <see cref="CandidateProfile"/> (WorkspaceId == null) onto it so today's data stays
/// visible after login. NEVER runs in Production unless <c>Dev:SeedDevUser=true</c> is set explicitly.
/// </summary>
public static class AuthSeeder
{
    public const string DevEmail = "dev@local";

    public static async Task SeedAsync(IServiceProvider services, IWebHostEnvironment env, IConfiguration config, ILogger logger)
    {
        var allowed = env.IsDevelopment() || config.GetValue("Dev:SeedDevUser", false);
        if (!allowed)
        {
            logger.LogInformation("AuthSeeder skipped: not Development and Dev:SeedDevUser is not set.");
            return;
        }

        var users = services.GetRequiredService<UserManager<AppUser>>();
        var db = services.GetRequiredService<OpportunityOsDbContext>();

        // 1. Ensure the dev user exists.
        var user = await users.FindByEmailAsync(DevEmail);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = DevEmail,
                Email = DevEmail,
                EmailConfirmed = true,
                DisplayName = "Dev",
            };
            var password = config["Dev:SeedPassword"] ?? "dev12345";
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogWarning("AuthSeeder could not create dev user: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("AuthSeeder created dev user {Email}.", DevEmail);
        }

        // 2. Ensure the dev user has a workspace.
        var workspace = await db.Workspaces.FirstOrDefaultAsync(w => w.UserId == user.Id);
        if (workspace is null)
        {
            workspace = new Workspace(user.Id, "Workspace dev");
            db.Workspaces.Add(workspace);
            await db.SaveChangesAsync();
        }

        // 3. Backfill orphan profiles (pre-auth data) onto the dev workspace.
        var orphans = await db.CandidateProfiles.Where(p => p.WorkspaceId == null).ToListAsync();
        foreach (var p in orphans) p.AssignWorkspace(workspace.Id);
        if (orphans.Count > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("AuthSeeder backfilled {Count} profile(s) onto the dev workspace.", orphans.Count);
        }

        // 4. Guarantee exactly one default profile in the dev workspace.
        var profiles = await db.CandidateProfiles.Where(p => p.WorkspaceId == workspace.Id).ToListAsync();
        if (profiles.Count > 0 && profiles.All(p => !p.IsDefault))
            profiles.OrderByDescending(p => p.CreatedAtUtc).First().SetDefault(true);
        var defaults = profiles.Where(p => p.IsDefault).OrderByDescending(p => p.CreatedAtUtc).ToList();
        for (var i = 1; i < defaults.Count; i++) defaults[i].SetDefault(false); // collapse to one
        await db.SaveChangesAsync();

        // 5. Transitional invariant: no profile should be left without a workspace.
        var remainingOrphans = await db.CandidateProfiles.CountAsync(p => p.WorkspaceId == null);
        if (remainingOrphans > 0)
            logger.LogWarning("AuthSeeder: {Count} candidate profile(s) still have a NULL workspace_id.", remainingOrphans);
    }
}
