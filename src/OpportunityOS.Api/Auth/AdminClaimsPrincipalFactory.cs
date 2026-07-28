using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpportunityOS.Infrastructure.Auth;

namespace OpportunityOS.Api.Auth;

/// <summary>
/// Adds an <c>is_admin</c> claim to the cookie principal for admin users, so the "Admin"
/// authorization policy resolves without a per-request DB lookup.
/// </summary>
public sealed class AdminClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser>
{
    public const string AdminClaim = "is_admin";

    public AdminClaimsPrincipalFactory(UserManager<AppUser> userManager, IOptions<IdentityOptions> options)
        : base(userManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.IsAdmin) identity.AddClaim(new Claim(AdminClaim, "true"));
        return identity;
    }
}
