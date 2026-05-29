using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OpportunityOS.Contracts;
using Xunit;

namespace OpportunityOS.IntegrationTests;

public sealed class ApiIntegrationTests : IClassFixture<OpportunityOsApiFactory>
{
    private readonly OpportunityOsApiFactory _factory;

    public ApiIntegrationTests(OpportunityOsApiFactory factory) => _factory = factory;

    [DbFact]
    public async Task Company_Crud_RoundTrips()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/companies", new CompanyRequest(
            "Nubank", "https://nubank.com.br", "https://boards.greenhouse.io/nubank", null,
            "Fintech", "Brazil", Priority: 4, Tags: new() { "fintech", "payments" }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<CompanyResponse>();
        Assert.NotNull(created);
        Assert.Equal("Strategic", created!.Priority);
        Assert.Equal("Manual", created.Source);

        var list = await client.GetFromJsonAsync<List<CompanyResponse>>("/api/companies");
        Assert.Single(list!);

        var get = await client.GetFromJsonAsync<CompanyResponse>($"/api/companies/{created.Id}");
        Assert.Equal("Nubank", get!.Name);

        var delete = await client.DeleteAsync($"/api/companies/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [DbFact]
    public async Task MatchEndpoint_PersistsHighScoreForFintechDotNetJob()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        // Profile required by the match endpoint.
        await client.PostAsJsonAsync("/api/candidate-profile", new CandidateProfileRequest(
            "Nícolas Serrano", "Desenvolvedor .NET Backend", "Backend .NET / pagamentos",
            "Brasil", "Pleno/Sênior", "pt-BR",
            CoreSkills: new() { ".NET", "C#", "AWS", "Kafka" },
            SecondarySkills: null, Domains: new() { "Pagamentos", "PIX" },
            PreferredRoles: null, PreferredContractTypes: null, PreferredLocations: null, Experiences: null));

        var companyResp = await client.PostAsJsonAsync("/api/companies", new CompanyRequest(
            "Sample Fintech", null, null, null, "Fintech", "Brazil", Priority: 4, Tags: null));
        var company = await companyResp.Content.ReadFromJsonAsync<CompanyResponse>();

        // Seed a job directly through the DB (no public create endpoint in Phase 1).
        Guid jobId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.OpportunityOsDbContext>();
            var job = new Domain.Entities.JobPosting(
                company!.Id, "ext-1", "Test", "Senior Backend Engineer (.NET / Payments)",
                "https://example.com/j1",
                "Build payments and PIX systems with .NET, C#, ASP.NET Core, Kafka, AWS. Remote, Brazil. Open Finance, fintech.",
                location: "Remote - Brazil", language: "en");
            db.JobPostings.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;
        }

        var matchResp = await client.PostAsync($"/api/jobs/{jobId}/match", null);
        Assert.Equal(HttpStatusCode.OK, matchResp.StatusCode);

        var match = await matchResp.Content.ReadFromJsonAsync<MatchResponse>();
        Assert.NotNull(match);
        Assert.True(match!.OverallScore >= 85, $"Expected >= 85 but was {match.OverallScore}");

        // The latest match is now retrievable.
        var latest = await client.GetFromJsonAsync<MatchResponse>($"/api/jobs/{jobId}/match");
        Assert.Equal(match.Id, latest!.Id);
    }
}
