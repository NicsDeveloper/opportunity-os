using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Infrastructure.Persistence;

namespace OpportunityOS.IntegrationTests;

/// <summary>
/// Boots the real API against the test database. Seeding is disabled so each
/// test owns its data; the schema is (re)created fresh per factory instance.
/// </summary>
public sealed class OpportunityOsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDb.ConnectionString);
        builder.UseSetting("SeedOnStartup", "false");
        builder.UseEnvironment("Testing");

        // Replace the real ATS providers with a deterministic one (no network).
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IJobSourceProvider>();
            services.AddScoped<IJobSourceProvider, TestJobSourceProvider>();

            // Deterministic Bacen CSV (no network) for integration tests.
            services.RemoveAll<Application.Bacen.IBacenPixParticipantsCsvProvider>();
            services.AddScoped<Application.Bacen.IBacenPixParticipantsCsvProvider, TestBacenCsvProvider>();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpportunityOsDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }
}
