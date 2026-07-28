using OpportunityOS.Application.AI;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using Xunit;

namespace OpportunityOS.UnitTests;

/// <summary>Hashing embedding determinism + the hybrid (heuristic + semantic) blend in the engine.</summary>
public sealed class SemanticMatchTests
{
    private static readonly HashingEmbeddingProvider Embedder = new();

    private static float[] Embed(string text) => Embedder.EmbedAsync(text, default).Result!;

    [Fact]
    public void Hashing_IsDeterministic_AndNormalized()
    {
        var a = Embed("backend developer .net c# aws");
        var b = Embed("backend developer .net c# aws");
        Assert.Equal(256, a.Length);
        Assert.Equal(a, b);                                   // deterministic across calls
        Assert.Equal(1.0, VectorMath.Cosine(a, a), 3);        // self-cosine ≈ 1 (L2-normalized)
    }

    [Fact]
    public void Hashing_SimilarText_ScoresHigherThanDifferent()
    {
        var profile = Embed("senior backend engineer .net c# aws microservices");
        var similar = Embed("backend developer .net c# aws kafka");
        var different = Embed("frontend react designer figma ui ux");
        Assert.True(VectorMath.Cosine(profile, similar) > VectorMath.Cosine(profile, different));
    }

    [Fact]
    public void Engine_SemanticBlend_RaisesAligned_AndLowersOrthogonal()
    {
        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job(
            "Senior Backend Engineer (.NET / Payments)",
            "Build payments and PIX systems with .NET, C#, ASP.NET Core, Kafka and AWS. Remote, Brazil. Fintech.",
            "Remote - Brazil", "en");

        var heuristicOnly = new HeuristicMatchEngine(new JobNormalizer())
            .Evaluate(profile, job).OverallScore;

        var hybrid = new HeuristicMatchEngine(new JobNormalizer(),
            new SemanticMatchOptions { Enabled = true, SemanticWeight = 0.4 });

        // Aligned embeddings (cosine 1) pull the score up; orthogonal (cosine 0) pull it down.
        profile.SetEmbedding(new float[] { 1, 0, 0, 0 }, "t");
        job.SetEmbedding(new float[] { 1, 0, 0, 0 }, "t");
        Assert.True(hybrid.Evaluate(profile, job).OverallScore >= heuristicOnly);

        job.SetEmbedding(new float[] { 0, 1, 0, 0 }, "t");
        Assert.True(hybrid.Evaluate(profile, job).OverallScore < heuristicOnly);
    }

    [Fact]
    public void Engine_SemanticDisabled_IgnoresEmbeddings()
    {
        var profile = TestData.BackendDotNetProfile();
        var job = TestData.Job("Backend (.NET)", "Backend .NET, C#, AWS. Remote, Brazil.", "Remote", "en");
        profile.SetEmbedding(new float[] { 0, 1 }, "t");
        job.SetEmbedding(new float[] { 1, 0 }, "t"); // orthogonal — would tank the score IF blended

        var off = new HeuristicMatchEngine(new JobNormalizer()).Evaluate(profile, job).OverallScore;
        var alsoOff = new HeuristicMatchEngine(new JobNormalizer(), new SemanticMatchOptions { Enabled = false })
            .Evaluate(profile, job).OverallScore;
        Assert.Equal(off, alsoOff);
    }
}
