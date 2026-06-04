using OpportunityOS.Application.Discovery;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class ConsultingSignalsTests
{
    [Fact]
    public void StrongConsultancy_ScoresHigh_WithExplainableSignals()
    {
        var (score, signals) = ConsultingSignals.Evaluate(
            "Consultoria de TI e outsourcing — transformação digital, squad .NET, trabalhe conosco",
            hasWebsite: true, multipleSources: true);
        Assert.True(score >= 80, $"expected high, got {score}");
        Assert.Contains(signals, s => s.Contains("consultoria"));
        Assert.Contains(signals, s => s.Contains("outsourcing"));
    }

    [Fact]
    public void PureSaaS_IsPenalized()
    {
        var (score, _) = ConsultingSignals.Evaluate(
            "Nossa plataforma SaaS de gestão financeira (produto)", hasWebsite: true, multipleSources: false);
        Assert.True(score < 30, $"expected low, got {score}");
    }

    [Fact]
    public void NoWebsite_IsPenalized()
    {
        var (withSite, _) = ConsultingSignals.Evaluate("consultoria .net", hasWebsite: true, multipleSources: false);
        var (noSite, _) = ConsultingSignals.Evaluate("consultoria .net", hasWebsite: false, multipleSources: false);
        Assert.True(withSite > noSite);
    }

    [Fact]
    public void Score_IsClampedTo0_100()
    {
        var (score, _) = ConsultingSignals.Evaluate(
            "consultoria outsourcing transformação digital carreiras .net nearshore software house clientes cases",
            hasWebsite: true, multipleSources: true);
        Assert.InRange(score, 0, 100);
    }
}
