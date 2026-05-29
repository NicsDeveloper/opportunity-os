using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.AI;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Ai;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class AiServicesTests
{
    private static readonly JobNormalizer Normalizer = new();
    private static HeuristicMatchEngine Engine() => new(Normalizer);

    // ---- JobUnderstandingService ----

    [Fact]
    public async Task JobUnderstanding_WithFakeLlm_ParsesAnalysis()
    {
        var audit = new CapturingLogStore();
        var svc = new JobUnderstandingService(new FakeLlmProvider(), audit, Normalizer,
            NullLogger<JobUnderstandingService>.Instance);

        var job = TestData.Job("Backend .NET", "Build payments with .NET and Kafka.", "Remote", "en");
        var result = await svc.AnalyzeAsync(job, CancellationToken.None);

        Assert.Contains(".NET", result.RequiredSkills);
        var log = Assert.Single(audit.Logs);
        Assert.True(log.Success);
        Assert.False(log.UsedFallback);
        Assert.Equal(Prompts.JobAnalysisVersion, log.PromptVersion);
    }

    [Fact]
    public async Task JobUnderstanding_WhenNotConfigured_UsesHeuristicFallback()
    {
        var audit = new CapturingLogStore();
        var svc = new JobUnderstandingService(StubLlmProvider.NotConfigured(), audit, Normalizer,
            NullLogger<JobUnderstandingService>.Instance);

        var job = TestData.Job("Senior Backend Engineer", "Pagamentos e PIX com .NET e Kafka.", "Remote", "en");
        var result = await svc.AnalyzeAsync(job, CancellationToken.None);

        Assert.Equal("Senior", result.Seniority);          // came from the normalizer
        Assert.Contains(".NET", result.RequiredSkills);
        Assert.True(audit.Logs.Single().UsedFallback);
    }

    [Fact]
    public async Task JobUnderstanding_WithInvalidJson_FallsBack_AndDoesNotThrow()
    {
        var audit = new CapturingLogStore();
        var svc = new JobUnderstandingService(StubLlmProvider.ReturningRaw("not even json"), audit, Normalizer,
            NullLogger<JobUnderstandingService>.Instance);

        var job = TestData.Job("Backend", "Build APIs with .NET.", "Remote", "en");
        var result = await svc.AnalyzeAsync(job, CancellationToken.None);

        Assert.NotNull(result);
        var log = Assert.Single(audit.Logs);
        Assert.False(log.Success);
        Assert.True(log.UsedFallback);
        Assert.Contains("Invalid JSON", log.ErrorMessage);
    }

    [Fact]
    public async Task JobUnderstanding_WhenLlmThrows_FallsBack()
    {
        var audit = new CapturingLogStore();
        var svc = new JobUnderstandingService(StubLlmProvider.Throwing(), audit, Normalizer,
            NullLogger<JobUnderstandingService>.Instance);

        var result = await svc.AnalyzeAsync(
            TestData.Job("Backend", "Build APIs with .NET.", "Remote", "en"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(audit.Logs.Single().UsedFallback);
    }

    // ---- CandidateFitAnalysisService ----

    [Fact]
    public async Task CandidateFit_WithFakeLlm_ProducesMatch()
    {
        var audit = new CapturingLogStore();
        var svc = new CandidateFitAnalysisService(new FakeLlmProvider(), audit, Engine(),
            NullLogger<CandidateFitAnalysisService>.Instance);

        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job("Backend .NET / Payments", "Payments, PIX, .NET, Kafka, AWS. Remote Brazil.", "Remote", "en");
        var analysis = new JobAnalysisResult(new() { ".NET" }, new(), new() { "Payments" },
            "Senior", "Remote", "en", new(), new(), "summary");

        var match = await svc.AnalyzeFitAsync(profile, job, analysis, CancellationToken.None);

        Assert.Equal(89, match.OverallScore);
        Assert.Equal(MatchRecommendation.Prioritize, match.Recommendation);
        Assert.Equal(job.Id, match.JobPostingId);
    }

    [Fact]
    public async Task CandidateFit_WhenNotConfigured_UsesHeuristicEngine()
    {
        var audit = new CapturingLogStore();
        var svc = new CandidateFitAnalysisService(StubLlmProvider.NotConfigured(), audit, Engine(),
            NullLogger<CandidateFitAnalysisService>.Instance);

        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job("Senior Backend Engineer (.NET / Payments)",
            "Build payments and PIX with .NET, C#, ASP.NET Core, Kafka, AWS. Remote, Brazil. Open Finance, fintech.",
            "Remote - Brazil", "en");
        var analysis = new JobAnalysisResult(new(), new(), new(), "Senior", "Remote", "en", new(), new(), "");

        var match = await svc.AnalyzeFitAsync(profile, job, analysis, CancellationToken.None);

        Assert.True(match.OverallScore >= 85);
        Assert.True(audit.Logs.Single().UsedFallback);
    }

    // ---- OutreachDraftService ----

    [Fact]
    public async Task Outreach_WithFakeLlm_ReturnsNonEmptyDrafts()
    {
        var audit = new CapturingLogStore();
        var svc = new OutreachDraftService(new FakeLlmProvider(), audit,
            NullLogger<OutreachDraftService>.Instance);

        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job("Backend .NET", "Payments with .NET.", "Remote", "en");
        var match = Engine().Evaluate(profile, job);
        var entity = match.ToEntityForTest(job.Id, profile.Id);

        var draft = await svc.GenerateAsync(profile, job, entity, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(draft.LinkedInMessage));
        Assert.False(string.IsNullOrWhiteSpace(draft.CoverLetter));
        Assert.False(string.IsNullOrWhiteSpace(draft.FollowUpMessage));
    }

    [Fact]
    public async Task Outreach_FallbackDraft_DoesNotInventSkills()
    {
        var audit = new CapturingLogStore();
        var svc = new OutreachDraftService(StubLlmProvider.NotConfigured(), audit,
            NullLogger<OutreachDraftService>.Instance);

        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job("Backend Engineer", "We need Rust and Go developers.", "Remote", "en");
        var match = Engine().Evaluate(profile, job).ToEntityForTest(job.Id, profile.Id);

        var draft = await svc.GenerateAsync(profile, job, match, CancellationToken.None);

        // Fallback only references the candidate's real skills, never the job-only Rust/Go.
        Assert.DoesNotContain("Rust", draft.LinkedInMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rust", draft.CoverLetter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".NET", draft.LinkedInMessage, StringComparison.OrdinalIgnoreCase);
    }
}
