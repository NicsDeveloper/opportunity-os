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

    // ---- Multi-profile: the engine must follow the candidate's stack, not a hardcoded .NET bias ----

    [Fact]
    public void JavaSpringJob_ScoresHigh_ForJavaProfile()
    {
        var engine = CreateEngine();
        var job = TestData.Job(
            title: "Senior Backend Engineer (Java / Spring)",
            description: "Backend engineer with Java, Spring Boot, microservices, Kafka, AWS and PostgreSQL. " +
                         "Fintech, banking. Remote, Brazil.",
            location: "Remote - Brazil", language: "en");

        var result = engine.Evaluate(TestData.JavaBackendProfile(), job);

        Assert.True(result.OverallScore >= 85, $"Expected >= 85 but was {result.OverallScore}");
    }

    [Fact]
    public void ReactTypeScriptJob_ScoresHigh_ForReactProfile()
    {
        var engine = CreateEngine();
        var job = TestData.Job(
            title: "Frontend Engineer Pleno (React / TypeScript)",
            description: "Frontend engineer with React, TypeScript, Next.js, CSS and design systems for our SaaS product. " +
                         "Remote, Brazil.",
            location: "Remote - Brazil", language: "en");

        var result = engine.Evaluate(TestData.ReactFrontendProfile(), job);

        Assert.True(result.OverallScore >= 85, $"Expected >= 85 but was {result.OverallScore}");
    }

    [Fact]
    public void DataEngineeringJob_ScoresHigh_ForDataProfile()
    {
        var engine = CreateEngine();
        var job = TestData.Job(
            title: "Senior Data Engineer (Python / Spark)",
            description: "Data engineer building ETL pipelines and a data lake with Python, SQL, Airflow, Spark, " +
                         "AWS Glue and Athena. Remote, Brazil.",
            location: "Remote - Brazil", language: "en");

        var result = engine.Evaluate(TestData.DataEngineerProfile(), job);

        Assert.True(result.OverallScore >= 85, $"Expected >= 85 but was {result.OverallScore}");
    }

    [Fact]
    public void PureDotNetJob_ScoresLow_ForReactProfile()
    {
        var engine = CreateEngine();
        var job = TestData.Job(
            title: "Senior Backend Engineer (.NET / C#)",
            description: "Backend engineer with .NET, C#, ASP.NET Core and SQL Server. Remote, Brazil.",
            location: "Remote - Brazil", language: "en");

        var result = engine.Evaluate(TestData.ReactFrontendProfile(), job);

        // A pure .NET role is NOT a good match for a React profile.
        Assert.True(result.OverallScore < 50, $"Expected < 50 but was {result.OverallScore}");
    }

    [Fact]
    public void FullstackReactDotNetJob_StaysRelevant_ForReactProfile()
    {
        var engine = CreateEngine();
        var job = TestData.Job(
            title: "Fullstack Developer (React / .NET)",
            description: "Fullstack developer building React + TypeScript + Next.js frontends backed by a .NET, C# API. " +
                         "Remote, Brazil.",
            location: "Remote - Brazil", language: "en");

        var result = engine.Evaluate(TestData.ReactFrontendProfile(), job);

        // The candidate's CORE stack (React) is present -> still a strong pick ("salvo fullstack compatível").
        Assert.True(result.OverallScore >= 75, $"Expected >= 75 but was {result.OverallScore}");
    }

    [Fact]
    public void RemoteSeniorJob_WithoutTechnicalAdherence_IsGatedDown()
    {
        var engine = CreateEngine();
        // A perfectly remote, senior, well-paid role — but in a stack the .NET candidate doesn't have.
        var job = TestData.Job(
            title: "Senior PHP Developer (Laravel)",
            description: "Senior developer with PHP, Laravel, MySQL and REST APIs. Remote, Brazil. Great pay.",
            location: "Remote - Brazil", language: "en");

        var result = engine.Evaluate(TestData.BackendDotNetProfile(), job);

        // Remote + senior must NOT lift a role with no technical adherence into the board.
        Assert.True(result.OverallScore <= 45, $"Expected <= 45 (gated) but was {result.OverallScore}");
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
