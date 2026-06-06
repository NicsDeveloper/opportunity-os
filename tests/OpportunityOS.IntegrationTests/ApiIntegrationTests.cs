using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        var client = await _factory.CreateAuthenticatedClientAsync();

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
        var client = await _factory.CreateAuthenticatedClientAsync();

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
        var client = await _factory.CreateAuthenticatedClientAsync();

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
        var client = await _factory.CreateAuthenticatedClientAsync();

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

    [DbFact]
    public async Task AiAnalyze_AutoCreatesOpportunity_ThenManualStatusAndFollowUp()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        await client.PostAsJsonAsync("/api/candidate-profile", new CandidateProfileRequest(
            "Nícolas", "Backend .NET", "x", "Brasil", "Pleno/Sênior", "pt-BR",
            new() { ".NET", "C#" }, null, new() { "Pagamentos" }, null, null, null, null));
        var jobId = await SeedJob("Senior Backend (.NET / Payments)",
            "Payments, PIX, .NET, C#, Kafka, AWS. Remote Brazil. Open Finance, fintech.");

        // FakeLlmProvider fit -> 89 (>= 70) so an Opportunity is auto-created.
        await client.PostAsync($"/api/jobs/{jobId}/ai/analyze", null);

        var opps = await client.GetFromJsonAsync<List<OpportunityResponse>>("/api/opportunities");
        var opp = Assert.Single(opps!);
        Assert.Equal(jobId, opp.JobPostingId);
        Assert.Equal("Analyzed", opp.Status);

        // Manual status change to a human-gated status is allowed via the API.
        var statusResp = await client.PutAsJsonAsync(
            $"/api/opportunities/{opp.Id}/status", new OpportunityStatusRequest("SentManually"));
        statusResp.EnsureSuccessStatusCode();
        var updated = await statusResp.Content.ReadFromJsonAsync<OpportunityResponse>();
        Assert.Equal("SentManually", updated!.Status);

        // Follow-up in the past shows up in the pending list.
        await client.PutAsJsonAsync($"/api/opportunities/{opp.Id}/follow-up",
            new OpportunityFollowUpRequest(DateTime.UtcNow.AddDays(-1)));
        var due = await client.GetFromJsonAsync<List<OpportunityResponse>>("/api/opportunities/follow-ups");
        Assert.Contains(due!, o => o.Id == opp.Id);
    }

    [DbFact]
    public async Task Recruiter_Crud_RoundTrips()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();
        var companyId = await CreateCompany();

        var create = await client.PostAsJsonAsync("/api/recruiters", new RecruiterRequest(
            companyId, "Ana Recruiter", "Tech Recruiter", "https://linkedin.com/in/ana", null, 1, "via referral"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var lead = await create.Content.ReadFromJsonAsync<RecruiterResponse>();
        Assert.Equal("Manual", lead!.Source);

        var list = await client.GetFromJsonAsync<List<RecruiterResponse>>("/api/recruiters");
        Assert.Single(list!);

        var del = await client.DeleteAsync($"/api/recruiters/{lead.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }

    [DbFact]
    public async Task Digest_Preview_ListsAnalyzedOpportunity_AndSendSkipsWithoutSmtp()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        await client.PostAsJsonAsync("/api/candidate-profile", new CandidateProfileRequest(
            "Nícolas", "Backend .NET", "x", "Brasil", "Pleno/Sênior", "pt-BR",
            new() { ".NET", "C#" }, null, new() { "Pagamentos" }, null, null, null, null));
        var jobId = await SeedJob("Senior Backend (.NET / Payments)",
            "Payments, PIX, .NET, C#, Kafka, AWS. Remote Brazil.");

        // Produces a match (FakeLlm fit -> 89), which becomes a digest item.
        await client.PostAsync($"/api/jobs/{jobId}/ai/analyze", null);

        var preview = await client.GetFromJsonAsync<DigestPreviewResponse>("/api/digest/preview");
        Assert.True(preview!.Total >= 1);
        Assert.Contains("Opportunity OS", preview.Html);

        // No SMTP configured in the test host -> records run, does not send.
        var send = await client.PostAsync("/api/digest/send", null);
        send.EnsureSuccessStatusCode();
        var result = await send.Content.ReadFromJsonAsync<DigestSendResponse>();
        Assert.False(result!.Sent);
        Assert.Contains("SMTP", result.Reason);
    }

    [DbFact]
    public async Task Bacen_Import_ThenPromote_AreIdempotent()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        // First import: 4 institutions created.
        var imp1 = await (await client.PostAsync("/api/bacen/pix-participants/import", null))
            .Content.ReadFromJsonAsync<BacenImportResponse>();
        Assert.Equal(4, imp1!.TotalRead);
        Assert.Equal(4, imp1.Created);
        Assert.Equal(0, imp1.Updated);

        // Second import: no duplicates — all updated.
        var imp2 = await (await client.PostAsync("/api/bacen/pix-participants/import", null))
            .Content.ReadFromJsonAsync<BacenImportResponse>();
        Assert.Equal(0, imp2!.Created);
        Assert.Equal(4, imp2.Updated);

        var list = await client.GetFromJsonAsync<List<BacenInstitutionResponse>>("/api/bacen/pix-participants");
        Assert.Equal(4, list!.Count);

        // Promote: only the 2 eligible (99PAY, BTG) become companies; coop + unauthorized skipped.
        var pro1 = await (await client.PostAsync("/api/bacen/pix-participants/promote-to-companies", null))
            .Content.ReadFromJsonAsync<BacenPromotionResponse>();
        Assert.Equal(2, pro1!.TotalEligible);
        Assert.Equal(2, pro1.CompaniesCreated);
        Assert.Equal(2, pro1.Skipped);

        // Promote again: no new companies — both updated.
        var pro2 = await (await client.PostAsync("/api/bacen/pix-participants/promote-to-companies", null))
            .Content.ReadFromJsonAsync<BacenPromotionResponse>();
        Assert.Equal(0, pro2!.CompaniesCreated);
        Assert.Equal(2, pro2.CompaniesUpdated);

        var companies = await client.GetFromJsonAsync<List<CompanyResponse>>("/api/companies");
        Assert.Equal(2, companies!.Count);
        Assert.Contains(companies, c => c.Source == "Bacen" && c.Priority == "Strategic"); // 99PAY/BTG
    }

    // Insert a match directly (bypassing the API/projection) with a controlled CreatedAtUtc — to
    // simulate pre-projection data and out-of-order arrivals.
    private async Task SeedMatch(Guid jobId, Guid profileId, int score, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.OpportunityOsDbContext>();
        var m = new Domain.Entities.OpportunityMatch(
            jobId, profileId, score, score, 0, 0, 0, 0, Domain.Enums.MatchRecommendation.Apply,
            new[] { "s" }, Array.Empty<string>(), Array.Empty<string>(), "r", "heuristic-test");
        typeof(Domain.Entities.OpportunityMatch)
            .GetProperty(nameof(Domain.Entities.OpportunityMatch.CreatedAtUtc))!.SetValue(m, createdAt);
        db.OpportunityMatches.Add(m);
        await db.SaveChangesAsync();
    }

    [DbFact]
    public async Task Backfill_RebuildsProjection_AndFeedReadsIt()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var a = await CreateProfile("Dev A", ".NET", "C#", "AWS");
        var jobId = await SeedJob("Senior Backend (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.");
        await SeedMatch(jobId, a, 90, DateTime.UtcNow); // match exists but projection does NOT

        // Feed reads the projection -> empty until we backfill.
        Assert.Empty(await Matches(client, a));

        var rebuild = await (await client.PostAsync("/api/jobs/rebuild-latest-matches", null))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, rebuild.GetProperty("processedPairs").GetInt32());
        Assert.Equal(1, rebuild.GetProperty("created").GetInt32());

        Assert.Contains(await Matches(client, a), m => m.JobPostingId == jobId);
    }

    [DbFact]
    public async Task Feed_UsesLatestProjection_NotStaleHighMatch()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var a = await CreateProfile("Dev A", ".NET", "C#", "AWS");
        var jobId = await SeedJob("Senior Backend (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.");
        // Old strong match (90) then a NEWER weak one (20). The feed must use the latest (20).
        await SeedMatch(jobId, a, 90, DateTime.UtcNow.AddMinutes(-10));
        await SeedMatch(jobId, a, 20, DateTime.UtcNow);
        await client.PostAsync("/api/jobs/rebuild-latest-matches", null);

        // minScore 60: the latest score is 20 -> the job must NOT appear (no stale-high leak).
        var feed = await client.GetFromJsonAsync<List<BestOpportunityResponse>>(
            $"/api/matches?minScore=60&take=200&candidateProfileId={a}");
        Assert.DoesNotContain(feed!, m => m.JobPostingId == jobId);

        // minScore 0: it appears, with the LATEST score (20).
        var all = await Matches(client, a);
        var row = Assert.Single(all.Where(m => m.JobPostingId == jobId));
        Assert.Equal(20, row.OverallScore);
    }

    private async Task<Guid> SeedJob(string title, string description)
    {
        var companyResp = await CreateCompany();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.OpportunityOsDbContext>();
        // Unique external id so a single test can seed multiple distinct jobs (provider+ext is unique).
        var ext = "ext-" + Guid.NewGuid().ToString("N")[..8];
        var job = new Domain.Entities.JobPosting(
            companyResp, ext, "Test", title, $"https://example.com/{ext}", description,
            location: "Remote", language: "en");
        db.JobPostings.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<Guid> CreateCompany()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("/api/companies", new CompanyRequest(
            "Acme", null, null, null, "Fintech", "Brazil", Priority: 3, Tags: null));
        var company = await resp.Content.ReadFromJsonAsync<CompanyResponse>();
        return company!.Id;
    }

    [DbFact]
    public async Task MatchEndpoint_PersistsHighScoreForFintechDotNetJob()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

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

    // ---- Multi-profile: matches/opportunities/feedback/applications are per CandidateProfile ----

    private async Task<Guid> CreateProfile(string fullName, params string[] coreSkills)
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("/api/candidate-profile", new CandidateProfileRequest(
            fullName, "headline", "summary", "Brasil", "Pleno/Sênior", "pt-BR",
            CoreSkills: coreSkills.ToList(), SecondarySkills: null, Domains: null, PreferredRoles: null,
            PreferredContractTypes: null, PreferredLocations: null, Experiences: null));
        var p = await resp.Content.ReadFromJsonAsync<CandidateProfileResponse>();
        return p!.Id;
    }

    [DbFact]
    public async Task SameJob_TwoProfiles_ProduceDistinctMatchesAndOpportunities()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var dotnet = await CreateProfile("DotNet Dev", ".NET", "C#", "ASP.NET Core", "AWS", "Kafka");
        var react = await CreateProfile("React Dev", "React", "TypeScript", "Next.js", "CSS");
        var jobId = await SeedJob(
            "Senior Backend Engineer (.NET / Payments)",
            "Build payments and PIX systems with .NET, C#, ASP.NET Core, Kafka and AWS. Remote, Brazil. Fintech.");

        var mDot = await (await client.PostAsync($"/api/jobs/{jobId}/match?candidateProfileId={dotnet}", null))
            .Content.ReadFromJsonAsync<MatchResponse>();
        var mReact = await (await client.PostAsync($"/api/jobs/{jobId}/match?candidateProfileId={react}", null))
            .Content.ReadFromJsonAsync<MatchResponse>();

        // Same job, different stacks -> different scores, one OpportunityMatch each.
        Assert.Equal(dotnet, mDot!.CandidateProfileId);
        Assert.Equal(react, mReact!.CandidateProfileId);
        Assert.True(mDot.OverallScore > mReact.OverallScore,
            $".NET ({mDot.OverallScore}) should beat React ({mReact.OverallScore}) on a .NET job");
        Assert.True(mDot.OverallScore >= 70, $"Expected .NET >= 70 but was {mDot.OverallScore}");

        // GET match is scoped per profile.
        var getDot = await client.GetFromJsonAsync<MatchResponse>($"/api/jobs/{jobId}/match?candidateProfileId={dotnet}");
        var getReact = await client.GetFromJsonAsync<MatchResponse>($"/api/jobs/{jobId}/match?candidateProfileId={react}");
        Assert.Equal(dotnet, getDot!.CandidateProfileId);
        Assert.Equal(react, getReact!.CandidateProfileId);

        // Opportunity is per (job, profile): the .NET match (>=70) created one for .NET only.
        var oppsDot = await client.GetFromJsonAsync<List<OpportunityResponse>>($"/api/opportunities?candidateProfileId={dotnet}");
        Assert.Single(oppsDot!);
        Assert.Equal(dotnet, oppsDot![0].CandidateProfileId);

        // Re-matching the same (job, profile) must NOT create a duplicate opportunity.
        await client.PostAsync($"/api/jobs/{jobId}/match?candidateProfileId={dotnet}", null);
        var oppsDot2 = await client.GetFromJsonAsync<List<OpportunityResponse>>($"/api/opportunities?candidateProfileId={dotnet}");
        Assert.Single(oppsDot2!);
    }

    [DbFact]
    public async Task Feedback_HidesJob_OnlyForThatProfile()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Two .NET profiles so the same job is strong for BOTH boards.
        var a = await CreateProfile("Dev A", ".NET", "C#", "ASP.NET Core", "AWS");
        var b = await CreateProfile("Dev B", ".NET", "C#", "ASP.NET Core", "AWS");
        var jobId = await SeedJob(
            "Senior Backend Engineer (.NET)",
            "Backend with .NET, C#, ASP.NET Core, AWS, Kafka. Remote, Brazil.");
        await client.PostAsync($"/api/jobs/{jobId}/match?candidateProfileId={a}", null);
        await client.PostAsync($"/api/jobs/{jobId}/match?candidateProfileId={b}", null);

        // Both boards show the job before any feedback.
        Assert.Contains(await Matches(client, a), m => m.JobPostingId == jobId);
        Assert.Contains(await Matches(client, b), m => m.JobPostingId == jobId);

        // Profile A marks it irrelevant -> hidden for A, still visible for B.
        await client.PostAsJsonAsync("/api/feedback", new FeedbackRequest(
            "Irrelevant", jobId, null, "stack diferente", a));

        Assert.DoesNotContain(await Matches(client, a), m => m.JobPostingId == jobId);
        Assert.Contains(await Matches(client, b), m => m.JobPostingId == jobId);
    }

    [DbFact]
    public async Task Applications_AreScopedPerProfile()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var a = await CreateProfile("Dev A", ".NET", "C#", "AWS");
        var b = await CreateProfile("Dev B", ".NET", "C#", "AWS");
        var jobId = await SeedJob("Senior Backend (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.");

        // Profile A applied; B did not.
        await client.PostAsJsonAsync("/api/feedback", new FeedbackRequest("Applied", jobId, null, null, a));

        var appsA = await client.GetFromJsonAsync<List<ApplicationResponse>>($"/api/applications?candidateProfileId={a}");
        var appsB = await client.GetFromJsonAsync<List<ApplicationResponse>>($"/api/applications?candidateProfileId={b}");
        Assert.Single(appsA!);
        Assert.Equal(jobId, appsA![0].JobPostingId);
        Assert.Empty(appsB!);
    }

    [DbFact]
    public async Task MatchesWithoutProfile_FallBackToDefault()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        // One profile, made default via the plural set-default endpoint.
        var only = await CreateProfile("Only Dev", ".NET", "C#", "AWS");
        await client.PostAsync($"/api/candidate-profiles/{only}/set-default", null);
        var jobId = await SeedJob("Senior Backend (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.");
        await client.PostAsync($"/api/jobs/{jobId}/match?candidateProfileId={only}", null);

        // No candidateProfileId -> default profile's board (back-compat with the single-profile flow).
        var def = await client.GetFromJsonAsync<List<BestOpportunityResponse>>("/api/matches?minScore=0&take=50");
        Assert.Contains(def!, m => m.JobPostingId == jobId);
    }

    private static async Task<List<BestOpportunityResponse>> Matches(HttpClient client, Guid profileId) =>
        await client.GetFromJsonAsync<List<BestOpportunityResponse>>(
            $"/api/matches?minScore=0&take=200&candidateProfileId={profileId}") ?? new();

    [DbFact]
    public async Task Rescore_SingleProfile_DoesNotMatchOthers_AllProfiles_Does()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var a = await CreateProfile("Dev A", ".NET", "C#", "AWS");
        var b = await CreateProfile("Dev B", "Java", "Spring Boot", "AWS");
        await SeedJob("Senior Backend (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.");

        // Default rescore targets ONE profile (A here, passed explicitly) -> no matches for B.
        var r1 = await client.PostAsync($"/api/jobs/rescore?candidateProfileId={a}", null);
        r1.EnsureSuccessStatusCode();
        Assert.NotEmpty(await Matches(client, a));
        Assert.Empty(await Matches(client, b));

        // allProfiles fans out -> B now has matches too.
        var r2 = await client.PostAsync("/api/jobs/rescore?allProfiles=true", null);
        r2.EnsureSuccessStatusCode();
        Assert.NotEmpty(await Matches(client, b));
    }

    [DbFact]
    public async Task GeneratedMessage_IsTiedToTheProfile_ItWasDraftedFor()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var a = await CreateProfile("Dev A", ".NET", "C#", "ASP.NET Core", "AWS", "Kafka");
        var jobId = await SeedJob(
            "Senior Backend Engineer (.NET / Payments)",
            "Build payments with .NET, C#, ASP.NET Core, Kafka, AWS. Remote, Brazil. Fintech.");

        var outreach = await client.PostAsync($"/api/jobs/{jobId}/ai/generate-outreach?candidateProfileId={a}", null);
        outreach.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.OpportunityOsDbContext>();
        var msg = await db.GeneratedMessages.SingleAsync(m => m.JobPostingId == jobId);
        Assert.Equal(a, msg.CandidateProfileId);
    }

    [DbFact]
    public async Task DebugProfileScores_ShowsDistinctScoresPerProfile()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var dotnet = await CreateProfile("DotNet Dev", ".NET", "C#", "ASP.NET Core", "AWS");
        var react = await CreateProfile("React Dev", "React", "TypeScript", "Next.js");
        var jobId = await SeedJob(
            "Senior Backend Engineer (.NET / Payments)",
            "Build payments with .NET, C#, ASP.NET Core, Kafka, AWS. Remote, Brazil. Fintech.");
        await client.PostAsync($"/api/jobs/{jobId}/match?candidateProfileId={dotnet}", null);
        await client.PostAsync($"/api/jobs/{jobId}/match?candidateProfileId={react}", null);

        var doc = await client.GetFromJsonAsync<JsonElement>($"/api/debug/job/{jobId}/profile-scores");
        var scores = doc.GetProperty("scores").EnumerateArray()
            .ToDictionary(e => e.GetProperty("candidateProfile").GetString()!,
                          e => e.GetProperty("score").GetInt32());
        Assert.True(scores["DotNet Dev"] > scores["React Dev"],
            $".NET ({scores["DotNet Dev"]}) should beat React ({scores["React Dev"]}) on a .NET job");
    }

    // ---------------- Auth Workspace MVP: auth, workspace, isolation ----------------

    private static async Task<Guid> CreateProfileWith(HttpClient client, string label)
    {
        var resp = await client.PostAsJsonAsync("/api/candidate-profiles", new CandidateProfileRequest(
            label, "headline", "summary", "Brasil", "Pleno/Sênior", "pt-BR",
            CoreSkills: new() { ".NET", "C#" }, SecondarySkills: null, Domains: null, PreferredRoles: null,
            PreferredContractTypes: null, PreferredLocations: null, Experiences: null, DisplayName: label));
        resp.EnsureSuccessStatusCode();
        var p = await resp.Content.ReadFromJsonAsync<CandidateProfileResponse>();
        return p!.Id;
    }

    [DbFact]
    public async Task Register_CreatesUserAndWorkspace()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("newuser@test.local");

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("newuser@test.local", me.GetProperty("email").GetString());
        var workspaceId = me.GetProperty("workspaceId").GetGuid();
        Assert.NotEqual(Guid.Empty, workspaceId);

        var ws = await client.GetFromJsonAsync<JsonElement>("/api/workspace/me");
        Assert.Equal(workspaceId, ws.GetProperty("workspaceId").GetGuid());
    }

    [DbFact]
    public async Task ProtectedEndpoints_Return401_WithoutLogin()
    {
        await _factory.ResetDatabaseAsync();
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/candidate-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/matches")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/applications")).StatusCode);
    }

    [DbFact]
    public async Task CandidateProfiles_AreScopedToWorkspace()
    {
        await _factory.ResetDatabaseAsync();
        var a = await _factory.CreateAuthenticatedClientAsync("a@test.local");
        var b = await _factory.CreateAuthenticatedClientAsync("b@test.local");

        var pa = await CreateProfileWith(a, "A profile");
        var pb = await CreateProfileWith(b, "B profile");

        var aList = await a.GetFromJsonAsync<List<CandidateProfileResponse>>("/api/candidate-profiles");
        Assert.Equal(new[] { pa }, aList!.Select(p => p.Id));

        var bList = await b.GetFromJsonAsync<List<CandidateProfileResponse>>("/api/candidate-profiles");
        Assert.Equal(new[] { pb }, bList!.Select(p => p.Id));
    }

    [DbFact]
    public async Task Matches_WithForeignProfile_Returns403()
    {
        await _factory.ResetDatabaseAsync();
        var a = await _factory.CreateAuthenticatedClientAsync("a@test.local");
        var b = await _factory.CreateAuthenticatedClientAsync("b@test.local");
        var pb = await CreateProfileWith(b, "B profile");

        var res = await a.GetAsync($"/api/matches?candidateProfileId={pb}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [DbFact]
    public async Task Feedback_WithForeignProfile_Returns403()
    {
        await _factory.ResetDatabaseAsync();
        var a = await _factory.CreateAuthenticatedClientAsync("a@test.local");
        var b = await _factory.CreateAuthenticatedClientAsync("b@test.local");
        var pb = await CreateProfileWith(b, "B profile");

        var res = await a.PostAsJsonAsync("/api/feedback",
            new FeedbackRequest("Irrelevant", Guid.NewGuid(), null, null, pb));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [DbFact]
    public async Task Applications_ReturnOnlyOwnWorkspace()
    {
        await _factory.ResetDatabaseAsync();
        var a = await _factory.CreateAuthenticatedClientAsync("a@test.local");
        var b = await _factory.CreateAuthenticatedClientAsync("b@test.local");
        var pa = await CreateProfileWith(a, "A profile");
        var pb = await CreateProfileWith(b, "B profile");
        var jobId = await SeedJob("Senior Backend (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.");

        // A marks the job as applied for its own profile.
        var fb = await a.PostAsJsonAsync("/api/feedback", new FeedbackRequest("Applied", jobId, null, null, pa));
        fb.EnsureSuccessStatusCode();

        var aApps = await a.GetFromJsonAsync<List<ApplicationResponse>>($"/api/applications?candidateProfileId={pa}");
        Assert.Contains(aApps!, x => x.JobPostingId == jobId);

        // B sees nothing — different workspace.
        var bApps = await b.GetFromJsonAsync<List<ApplicationResponse>>($"/api/applications?candidateProfileId={pb}");
        Assert.Empty(bApps!);
    }

    [DbFact]
    public async Task SwitchingProfile_ChangesFeed()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("switch@test.local");
        var p1 = await CreateProfileWith(client, "P1");
        var p2 = await CreateProfileWith(client, "P2");
        var job1 = await SeedJob("Backend A (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.");
        var job2 = await SeedJob("Backend B (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.");
        await SeedMatch(job1, p1, 90, DateTime.UtcNow);
        await SeedMatch(job2, p2, 90, DateTime.UtcNow);
        await client.PostAsync("/api/jobs/rebuild-latest-matches", null);

        var feed1 = await Matches(client, p1);
        Assert.Contains(feed1, m => m.JobPostingId == job1);
        Assert.DoesNotContain(feed1, m => m.JobPostingId == job2);

        var feed2 = await Matches(client, p2);
        Assert.Contains(feed2, m => m.JobPostingId == job2);
        Assert.DoesNotContain(feed2, m => m.JobPostingId == job1);
    }

    [DbFact]
    public async Task Logout_EndsSession()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync("logout@test.local");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    // ---------------- LinkedIn PDF profile import ----------------

    private static async Task<HttpResponseMessage> UploadPdf(
        HttpClient client, string fileName = "alex.pdf", string contentType = "application/pdf")
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }); // "%PDF-"
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);
        return await client.PostAsync("/api/profile-imports/linkedin-pdf", form);
    }

    [DbFact]
    public async Task ProfileImport_Upload_WithoutLogin_Returns401()
    {
        await _factory.ResetDatabaseAsync();
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadPdf(anon)).StatusCode);
    }

    [DbFact]
    public async Task ProfileImport_Upload_NonPdf_Returns400()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();
        var res = await UploadPdf(client, "resume.txt", "text/plain");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [DbFact]
    public async Task ProfileImport_Upload_Valid_ReturnsImportAndDraft()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();

        var res = await UploadPdf(client);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<UploadLinkedInProfilePdfResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.ImportId);
        Assert.Equal("Alex Backend Developer", body.ParsedProfile.FullName);
        Assert.Contains(".NET", body.Draft.CoreSkills);
        Assert.DoesNotContain("Java", body.Draft.CoreSkills);
    }

    [DbFact]
    public async Task ProfileImport_Get_IsScopedToWorkspace()
    {
        await _factory.ResetDatabaseAsync();
        var a = await _factory.CreateAuthenticatedClientAsync("a@test.local");
        var b = await _factory.CreateAuthenticatedClientAsync("b@test.local");

        var up = await (await UploadPdf(a)).Content.ReadFromJsonAsync<UploadLinkedInProfilePdfResponse>();
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/profile-imports/{up!.ImportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/profile-imports/{up.ImportId}")).StatusCode);
    }

    [DbFact]
    public async Task ProfileImport_Apply_CreatesDefaultProfile_AndFeedAccepts()
    {
        await _factory.ResetDatabaseAsync();
        var client = await _factory.CreateAuthenticatedClientAsync();
        var up = await (await UploadPdf(client)).Content.ReadFromJsonAsync<UploadLinkedInProfilePdfResponse>();

        var apply = await client.PostAsJsonAsync($"/api/profile-imports/{up!.ImportId}/apply",
            new ApplyProfileImportRequest(up.Draft, SetAsDefault: true));
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var profile = await apply.Content.ReadFromJsonAsync<CandidateProfileResponse>();
        Assert.True(profile!.IsDefault);

        var list = await client.GetFromJsonAsync<List<CandidateProfileResponse>>("/api/candidate-profiles");
        Assert.Contains(list!, p => p.Id == profile.Id);

        // The new profile is a valid feed scope (200, not 403).
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/matches?candidateProfileId={profile.Id}")).StatusCode);
    }

    [DbFact]
    public async Task ProfileImport_Apply_ForeignImport_Returns404()
    {
        await _factory.ResetDatabaseAsync();
        var a = await _factory.CreateAuthenticatedClientAsync("a@test.local");
        var b = await _factory.CreateAuthenticatedClientAsync("b@test.local");
        var up = await (await UploadPdf(a)).Content.ReadFromJsonAsync<UploadLinkedInProfilePdfResponse>();

        var res = await b.PostAsJsonAsync($"/api/profile-imports/{up!.ImportId}/apply",
            new ApplyProfileImportRequest(up.Draft, SetAsDefault: true));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
