using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Enums;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class DiscoveryRankTests
{
    private readonly DiscoveryRankService _svc = new();

    [Fact]
    public void Freshness_DecaysWithAge()
    {
        Assert.Equal(100, _svc.FreshnessScore(DateTime.UtcNow.AddDays(-1)));
        Assert.Equal(70, _svc.FreshnessScore(DateTime.UtcNow.AddDays(-20)));
        Assert.Equal(10, _svc.FreshnessScore(DateTime.UtcNow.AddDays(-400)));
    }

    [Fact]
    public void CompanyPriority_MapsToScore()
    {
        Assert.Equal(100, _svc.CompanyPriorityScore(CompanyPriority.Strategic));
        Assert.Equal(25, _svc.CompanyPriorityScore(CompanyPriority.Low));
    }

    [Fact]
    public void DiscoveryRank_IsWeightedBlend_WithinBounds()
    {
        var top = _svc.ComputeDiscoveryRank(new DiscoveryRankInput(
            FitScore: 90, SourceConfidenceScore: 90, EffectiveDateUtc: DateTime.UtcNow.AddDays(-1),
            CompanyPriority: CompanyPriority.Strategic, UserFeedbackBoost: 100));
        Assert.InRange(top, 90, 100);

        var weak = _svc.ComputeDiscoveryRank(new DiscoveryRankInput(
            FitScore: 30, SourceConfidenceScore: 25, EffectiveDateUtc: DateTime.UtcNow.AddDays(-300),
            CompanyPriority: CompanyPriority.Low, UserFeedbackBoost: 0));
        Assert.InRange(weak, 0, 40);
        Assert.True(top > weak);
    }

    [Fact]
    public void FreshRecentRole_CanOutrankOlderHigherFit()
    {
        // Older, slightly higher fit, weak source vs fresh, official source.
        var oldStrong = _svc.ComputeDiscoveryRank(new DiscoveryRankInput(
            80, 35, DateTime.UtcNow.AddDays(-200), CompanyPriority.Low));
        var freshOfficial = _svc.ComputeDiscoveryRank(new DiscoveryRankInput(
            74, 90, DateTime.UtcNow.AddDays(-1), CompanyPriority.High));
        Assert.True(freshOfficial > oldStrong);
    }
}
