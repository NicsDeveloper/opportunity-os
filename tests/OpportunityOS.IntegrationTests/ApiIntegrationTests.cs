using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
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
    public async Task DiscoverEndpoint_PersistsJobs_AndDedupesOnSecondRun()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/companies", new CompanyRequest(
            "Acme", null, "https://boards.greenhouse.io/acme", null, "Fintech", "Brazil",
            Priority: 3, Tags: null));

        var first = await client.PostAsJsonAsync("/api/jobs/discover", new DiscoverRequest(null));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<DiscoveryResultResponse>();
        Assert.Equal(1, firstResult!.JobsDiscovered);
        Assert.Equal(0, firstResult.JobsUpdated);

        // Second run: same posting is refreshed, not duplicated.
        var second = await client.PostAsJsonAsync("/api/jobs/discover", new DiscoverRequest(null));
        var secondResult = await second.Content.ReadFromJsonAsync<DiscoveryResultResponse>();
        Assert.Equal(0, secondResult!.JobsDiscovered);
        Assert.Equal(1, secondResult.JobsUpdated);

        var jobs = await client.GetFromJsonAsync<List<JobPostingResponse>>("/api/jobs");
        Assert.Single(jobs!);
        Assert.Equal("Test", jobs![0].SourceProvider);
    }

    [DbFact]
    public async Task AiAnalyze_PersistsMatch_AndPromptLog_ThenOutreachDraft()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/candidate-profile", new CandidateProfileRequest(
            "Nícolas Serrano", "Desenvolvedor .NET Backend", "Backend .NET / pagamentos",
            "Brasil", "Pleno/Sênior", "pt-BR",
            CoreSkills: new() { ".NET", "C#", "AWS", "Kafka" }, SecondarySkills: null,
            Domains: new() { "Pagamentos", "PIX" }, PreferredRoles: null,
            PreferredContractTypes: null, PreferredLocations: null, Experiences: null));

        var jobId = await SeedJob(
            "Senior Backend Engineer (.NET / Payments)",
            "Build payments and PIX systems with .NET, C#, ASP.NET Core, Kafka, AWS. Remote, Brazil. Open Finance, fintech.");

        // analyze -> persists OpportunityMatch + a PromptExecutionLog (FakeLlmProvider).
        var analyze = await client.PostAsync($"/api/jobs/{jobId}/ai/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyze.StatusCode);
        var ai = await analyze.Content.ReadFromJsonAsync<AiAnalyzeResponse>();
        Assert.Contains(".NET", ai!.Analysis.RequiredSkills);
        Assert.True(ai.Match.OverallScore >= 60);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.OpportunityOsDbContext>();
            Assert.True(await db.PromptExecutionLogs.AnyAsync());
        }

        // outreach -> a Draft GeneratedMessage.
        var outreach = await client.PostAsync($"/api/jobs/{jobId}/ai/generate-outreach", null);
        Assert.Equal(HttpStatusCode.OK, outreach.StatusCode);
        var msg = await outreach.Content.ReadFromJsonAsync<GeneratedMessageResponse>();
        Assert.Equal("Draft", msg!.Status);
        Assert.False(string.IsNullOrWhiteSpace(msg.LinkedInMessage));
    }

    [DbFact]
    public async Task Outreach_BelowScoreGate_Returns422()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/candidate-profile", new CandidateProfileRequest(
            "Nícolas", "Backend", "x", "Brasil", "Pleno", "pt-BR",
            new() { ".NET" }, null, null, null, null, null, null));

        var jobId = await SeedJob("Frontend React Developer", "React, Angular, CSS. Onsite São Paulo.");

        // Pre-persist a low-score match so the gate triggers deterministically.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.OpportunityOsDbContext>();
            var profileId = db.CandidateProfiles.Select(p => p.Id).First();
            db.OpportunityMatches.Add(new Domain.Entities.OpportunityMatch(
                jobId, profileId, 30, 10, 25, 65, 35, 90, Domain.Enums.MatchRecommendation.Ignore,
                new[] { "x" }, new[] { "y" }, Array.Empty<string>(), "low"));
            await db.SaveChangesAsync();
        }

        var outreach = await client.PostAsync($"/api/jobs/{jobId}/ai/generate-outreach", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, outreach.StatusCode);
    }

    private async Task<Guid> SeedJob(string title, string description)
    {
        var companyResp = await CreateCompany();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.OpportunityOsDbContext>();
        var job = new Domain.Entities.JobPosting(
            companyResp, "ext", "Test", title, "https://example.com/j", description,
            location: "Remote", language: "en");
        db.JobPostings.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<Guid> CreateCompany()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/companies", new CompanyRequest(
            "Acme", null, null, null, "Fintech", "Brazil", Priority: 3, Tags: null));
        var company = await resp.Content.ReadFromJsonAsync<CompanyResponse>();
        return company!.Id;
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
