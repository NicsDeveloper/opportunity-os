using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.Pipeline;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class OpportunityPipelineTests
{
    private static OpportunityMatch Match(int overall) =>
        new(Guid.NewGuid(), Guid.NewGuid(), overall, 0, 0, 0, 0, 0,
            MatchRecommendation.Apply, new List<string>(), new List<string>(), new List<string>(), "r");

    private static OpportunityPipeline Build(FakeOpportunityStore store) =>
        new(store, NullLogger<OpportunityPipeline>.Instance);

    [Theory]
    [InlineData(70, true)]
    [InlineData(85, true)]
    [InlineData(69, false)]
    [InlineData(0, false)]
    public async Task EnsureForMatch_CreatesOnlyWhenScoreAtLeast70(int score, bool shouldCreate)
    {
        var store = new FakeOpportunityStore();
        var created = await Build(store).EnsureForMatchAsync(Match(score), CancellationToken.None);

        Assert.Equal(shouldCreate, created is not null);
        Assert.Equal(shouldCreate ? 1 : 0, store.Opportunities.Count);
    }

    [Fact]
    public async Task EnsureForMatch_DoesNotDuplicateForSameJob()
    {
        var store = new FakeOpportunityStore();
        var pipeline = Build(store);
        var match = Match(80);

        await pipeline.EnsureForMatchAsync(match, CancellationToken.None);
        await pipeline.EnsureForMatchAsync(match, CancellationToken.None);

        Assert.Single(store.Opportunities);
    }

    [Fact]
    public async Task EnsureForMatch_CreatesWithAnalyzedStatus()
    {
        var store = new FakeOpportunityStore();
        await Build(store).EnsureForMatchAsync(Match(90), CancellationToken.None);
        Assert.Equal(OpportunityStatus.Analyzed, store.Opportunities[0].Status);
    }

    [Fact]
    public async Task MarkMessageGenerated_AdvancesToReadyForHumanReview()
    {
        var store = new FakeOpportunityStore();
        var pipeline = Build(store);
        var match = Match(90);
        await pipeline.EnsureForMatchAsync(match, CancellationToken.None);

        await pipeline.MarkMessageGeneratedAsync(match.JobPostingId, CancellationToken.None);

        Assert.Equal(OpportunityStatus.ReadyForHumanReview, store.Opportunities[0].Status);
    }

    [Fact]
    public void System_CannotAdvanceBeyondReadyForHumanReview()
    {
        var opp = Opportunity.Create(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => opp.AdvanceTo(OpportunityStatus.SentManually));
    }

    [Fact]
    public void Human_CanSetSentManually()
    {
        var opp = Opportunity.Create(Guid.NewGuid());
        opp.SetStatusManually(OpportunityStatus.SentManually);
        Assert.Equal(OpportunityStatus.SentManually, opp.Status);
    }
}
