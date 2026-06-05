using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using Xunit;

namespace OpportunityOS.UnitTests;

/// <summary>
/// Regression: stack families must match on WORD BOUNDARIES. The classic bug was the token
/// "java" matching "javascript", which made a C# job score 100% technical for a Java profile.
/// </summary>
public sealed class StackTaxonomyTests
{
    [Fact]
    public void Java_DoesNotMatch_JavaScript()
    {
        var families = StackTaxonomy.FamiliesIn(
            "Senior C# Developer. We build with .NET, ASP.NET Core and a JavaScript/React front-end.");
        Assert.Contains(StackFamily.DotNet, families);
        Assert.Contains(StackFamily.Frontend, families); // javascript/react
        Assert.DoesNotContain(StackFamily.Java, families);  // <- the bug
    }

    [Fact]
    public void Java_StillMatches_RealJavaSignals()
    {
        var families = StackTaxonomy.FamiliesIn("Backend engineer with Java and Spring Boot, JVM tuning.");
        Assert.Contains(StackFamily.Java, families);
    }

    [Fact]
    public void Go_DoesNotMatch_GoogleOrMongo()
    {
        var families = StackTaxonomy.FamiliesIn("Experience with Google Cloud and MongoDB.");
        Assert.DoesNotContain(StackFamily.Go, families);
    }

    [Fact]
    public void JavaProfile_OnCSharpJobMentioningJavaScript_DoesNotScoreAsTopMatch()
    {
        var engine = new HeuristicMatchEngine(new JobNormalizer());
        var profile = TestData.JavaBackendProfile();
        var job = TestData.Job(
            title: "Senior C# Developer with Fintech experience",
            description: "Build payments systems with .NET, C#, ASP.NET Core, Kafka and AWS. " +
                         "Some JavaScript on the front-end. Remote, Brazil.",
            location: "Remote - Brazil",
            language: "en");

        var result = engine.Evaluate(profile, job);

        // The candidate's core (Java) is NOT in this job -> must never be a top pick.
        Assert.True(result.OverallScore < 60, $"Expected < 60 for a Java profile on a C# job, but was {result.OverallScore}");
    }
}
