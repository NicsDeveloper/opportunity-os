using System.Net;
using System.Net.Http.Json;
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

            // Deterministic PDF text (no real PDF binary) for the LinkedIn importer tests.
            services.RemoveAll<Application.Import.IPdfTextExtractor>();
            services.AddScoped<Application.Import.IPdfTextExtractor, TestPdfTextExtractor>();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpportunityOsDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public const string PrimaryEmail = "primary@test.local";
    public const string TestPassword = "password123";

    /// <summary>
    /// A client authenticated (cookie) as <paramref name="email"/>. Idempotent: registers the user,
    /// or logs in if it already exists — so multiple clients in the same test share one workspace,
    /// and it survives a <see cref="ResetDatabaseAsync"/>. Use distinct emails to simulate two users.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email = PrimaryEmail, string password = TestPassword)
    {
        var client = CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password, displayName = email.Split('@')[0] });
        if (register.StatusCode == HttpStatusCode.Conflict)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            login.EnsureSuccessStatusCode();
        }
        else
        {
            register.EnsureSuccessStatusCode();
        }
        return client;
    }
}
