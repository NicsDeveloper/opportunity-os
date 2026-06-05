using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpportunityOS.Api.Auth;
using OpportunityOS.Api.Data;
using OpportunityOS.Api.Endpoints;
using OpportunityOS.Application.Auth;
using OpportunityOS.Application.Projections;
using OpportunityOS.Infrastructure;
using OpportunityOS.Infrastructure.Auth;
using OpportunityOS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

// Auth: ASP.NET Core Identity + cookie. The HTTP-backed current-user context overrides the
// "system" default registered by AddInfrastructure, so per-profile queries are workspace-scoped.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
    {
        // Simple-but-hashed (spec §9.4): relax composition rules, keep a sane minimum length.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<OpportunityOsDbContext>()
    .AddClaimsPrincipalFactory<AdminClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "oos.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always          // https-only in production
        : CookieSecurePolicy.SameAsRequest;  // allow http in dev/testing
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    // This is an API: never redirect to a login page — answer with status codes.
    options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

// "System" policy guards cost-bearing / global-mutating endpoints. MVP = any authenticated user
// (no real admin role yet). Dev:OpenSystemEndpoints=true relaxes it to anonymous in Development only.
var openSystem = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue("Dev:OpenSystemEndpoints", false);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("System", policy =>
    {
        if (openSystem) policy.RequireAssertion(_ => true);
        else policy.RequireAuthenticatedUser();
    });
    // Operational admin panel + actions (sweeps, system controls).
    options.AddPolicy("Admin", policy => policy.RequireClaim(AdminClaimsPrincipalFactory.AdminClaim, "true"));
});

var app = builder.Build();

// Translate cross-tenant profile access into 403 (the workspace-scoped provider throws this).
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (ForbiddenProfileAccessException)
    {
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "forbidden_profile", message = "Profile not in your workspace." });
        }
    }
});

// Apply migrations and seed on startup (Phase 1 convenience for local/dev).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OpportunityOsDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue("SeedOnStartup", true))
    {
        await DatabaseSeeder.SeedAsync(db);

        // Provision the dev user + workspace and backfill pre-auth profiles (Development/flag only).
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AuthSeeder");
        await AuthSeeder.SeedAsync(scope.ServiceProvider, app.Environment, app.Configuration, logger);
    }

    // One-time backfill of the LatestOpportunityMatch projection: when it's empty but matches
    // already exist (e.g. right after this migration), rebuild it from the existing matches.
    if (!await db.LatestOpportunityMatches.AnyAsync() && await db.OpportunityMatches.AnyAsync())
        await scope.ServiceProvider.GetRequiredService<ILatestOpportunityMatchProjection>().BackfillAsync(default);
}

app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Opportunity OS", status = "ok" }))
   .WithTags("Health");

app.MapAuthEndpoints();
app.MapWorkspaceEndpoints();
app.MapAdminEndpoints();
app.MapCandidateProfileEndpoints();
app.MapCompanyEndpoints();
app.MapJobEndpoints();
app.MapAiEndpoints();
app.MapOpportunityEndpoints();
app.MapRecruiterEndpoints();
app.MapDigestEndpoints();
app.MapBacenEndpoints();
app.MapDashboardEndpoints();
app.MapDiscoveryEndpoints();
app.MapFeedbackEndpoints();
app.MapConsultingRadarEndpoints();
app.MapDebugEndpoints();

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program { }
