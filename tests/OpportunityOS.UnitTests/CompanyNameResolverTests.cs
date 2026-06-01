using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Providers;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class CompanyNameResolverTests
{
    private readonly CompanyNameResolver _svc = new();

    [Fact]
    public void ExtractsCompany_FromAtClause_StrippingAggregatorSuffix()
    {
        // "Senior C# Developer at MARGO - Jobgether" -> MARGO (via Jobgether aggregator)
        var r = _svc.Resolve("Senior C# Developer at MARGO - Jobgether",
            "https://jobgether.com/offer/123", "jobgether.com", SourceType.Aggregator);
        Assert.Equal("MARGO", r.RealCompanyName);
        Assert.Equal("jobgether.com", r.SourceName);
        Assert.Equal(SourceType.Aggregator, r.SourceType);
    }

    [Fact]
    public void ExtractsCompany_FromBracketTag()
    {
        var r = _svc.Resolve("[FORTIS SRT] .NET/C# Backend Developer",
            "https://www.reddit.com/r/forhire/x", "reddit.com", SourceType.SocialIndexed);
        Assert.Equal("FORTIS SRT", r.RealCompanyName);
    }

    [Fact]
    public void OfficialAtsHost_DerivesCompanyFromHost_HighConfidence()
    {
        var r = _svc.Resolve("Software Engineer", "https://boards.greenhouse.io/stone/jobs/7",
            "boards.greenhouse.io", SourceType.OfficialAts);
        Assert.Equal("stone", r.RealCompanyName);
        Assert.True(r.Confidence >= 80);
    }

    [Fact]
    public void Aggregator_WithoutCompanyInTitle_DoesNotGuessHost()
    {
        var r = _svc.Resolve("NET Developer Jobs (NOW HIRING)",
            "https://www.indeed.com/q-net-developer.html", "indeed.com", SourceType.Aggregator);
        Assert.Null(r.RealCompanyName);
        Assert.Equal(0, r.Confidence);
        Assert.Contains("não confirmada", r.Reason);
    }

    [Fact]
    public void GupySubdomain_DerivesCompany()
    {
        var r = _svc.Resolve("Desenvolvedor .NET", "https://fcamara.gupy.io/jobs/123",
            "fcamara.gupy.io", SourceType.OfficialAts);
        Assert.Equal("fcamara", r.RealCompanyName);
    }
}
