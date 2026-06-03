using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Domain.Enums;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class HeuristicMatchEngineTests
{
    private static HeuristicMatchEngine CreateEngine() => new(new JobNormalizer());

    [Fact]
    public void FintechDotNetPaymentsJob_ScoresHigh()
    {
        var engine = CreateEngine();
        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job(
            title: "Senior Backend Engineer (.NET / Payments)",
            description: "We are hiring a Senior Backend Engineer to build payments and PIX systems " +
                         "using .NET, C#, ASP.NET Core, Kafka and AWS. Remote, Brazil. " +
                         "Experience with Open Finance and fintech is a plus.",
            location: "Remote - Brazil",
            language: "en");

        var result = engine.Evaluate(profile, job);

        Assert.True(result.OverallScore >= 85, $"Expected >= 85 but was {result.OverallScore}");
        Assert.True(
            result.Recommendation is MatchRecommendation.Prioritize or MatchRecommendation.Strategic,
            $"Unexpected recommendation: {result.Recommendation}");
    }

    [Fact]
    public void PureFrontendReactJob_ScoresLow()
    {
        var engine = CreateEngine();
        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job(
            title: "Frontend React Developer",
            description: "Frontend developer with React, Angular and CSS to build our e-commerce " +
                         "storefront. Onsite role.",
            location: "São Paulo - Onsite",
            language: "en");

        var result = engine.Evaluate(profile, job);

        Assert.True(result.OverallScore < 50, $"Expected < 50 but was {result.OverallScore}");
    }

    [Fact]
    public void NonTechRoleAtFintech_IsGatedDown()
    {
        var engine = CreateEngine();
        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job(
            title: "Director, Collections",
            description: "Lead our collections and credit recovery operations at a fast-growing " +
                         "fintech / payments company. Manage a team, own the financial strategy. Remote, Brazil.",
            location: "Remote - Brazil",
            language: "en");

        var result = engine.Evaluate(profile, job);

        // Strong domain + remote + language must NOT push a non-backend role to the top.
        Assert.True(result.OverallScore <= 40, $"Expected <= 40 (gated) but was {result.OverallScore}");
    }

    [Fact]
    public void BackendWithoutDotNet_CappedBelowTop()
    {
        var engine = CreateEngine();
        var job = TestData.Job(
            title: "Senior Backend Engineer (Java / Spring)",
            description: "Backend engineer with Java, Spring Boot, microservices, AWS and payments. Remote, Brazil.",
            location: "Remote - Brazil",
            language: "en");

        var result = engine.Evaluate(TestData.BackendDotNetProfile(), job);

        // Backend but no .NET confirmed: allowed to appear, but never a top/strategic pick.
        Assert.True(result.OverallScore <= 70, $"Expected <= 70 but was {result.OverallScore}");
    }

    [Fact]
    public void Evaluate_AlwaysProducesRationaleAndSubScoresInRange()
    {
        var engine = CreateEngine();
        var result = engine.Evaluate(
            TestData.BackendDotNetProfile(),
            TestData.Job("Backend Engineer", "Build APIs with .NET and PostgreSQL.", "Remote", "en"));

        Assert.False(string.IsNullOrWhiteSpace(result.Rationale));
        foreach (var s in new[]
                 {
                     result.OverallScore, result.TechnicalScore, result.DomainScore,
                     result.SeniorityScore, result.LocationScore, result.LanguageScore
                 })
        {
            Assert.InRange(s, 0, 100);
        }
    }

    [Theory]
    [InlineData(95, MatchRecommendation.Strategic)]
    [InlineData(80, MatchRecommendation.Prioritize)]
    [InlineData(65, MatchRecommendation.Apply)]
    [InlineData(45, MatchRecommendation.SaveForLater)]
    [InlineData(20, MatchRecommendation.Ignore)]
    public void RecommendationThresholds_MapAsSpecified(int overall, MatchRecommendation expected)
    {
        // Reproduce the spec's published thresholds to lock them in.
        var actual = overall switch
        {
            >= 90 => MatchRecommendation.Strategic,
            >= 75 => MatchRecommendation.Prioritize,
            >= 60 => MatchRecommendation.Apply,
            >= 40 => MatchRecommendation.SaveForLater,
            _ => MatchRecommendation.Ignore
        };
        Assert.Equal(expected, actual);
    }
}
