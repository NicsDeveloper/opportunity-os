using Microsoft.EntityFrameworkCore;
using OpportunityOS.Api.Data;
using OpportunityOS.Api.Endpoints;
using OpportunityOS.Infrastructure;
using OpportunityOS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations and seed on startup (Phase 1 convenience for local/dev).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OpportunityOsDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue("SeedOnStartup", true))
        await DatabaseSeeder.SeedAsync(db);
}

app.MapOpenApi();

app.MapGet("/", () => Results.Ok(new { service = "Opportunity OS", status = "ok" }))
   .WithTags("Health");

app.MapCandidateProfileEndpoints();
app.MapCompanyEndpoints();
app.MapJobEndpoints();
app.MapAiEndpoints();
app.MapOpportunityEndpoints();
app.MapRecruiterEndpoints();

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program { }
