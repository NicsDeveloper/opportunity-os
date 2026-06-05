using Microsoft.AspNetCore.Identity;

namespace OpportunityOS.Infrastructure.Auth;

/// <summary>
/// The authenticated user. Built on ASP.NET Core Identity (<see cref="IdentityUser{TKey}"/>),
/// which already supplies <c>Email</c>, normalized email, the hashed password and security stamp.
/// We add a friendly <see cref="DisplayName"/> and lifecycle timestamps (spec §10).
/// Each user owns exactly one <see cref="OpportunityOS.Domain.Entities.Workspace"/> (1:1 in the MVP).
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Operational admin: may access the admin panel and trigger sweeps / system actions.</summary>
    public bool IsAdmin { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
}
