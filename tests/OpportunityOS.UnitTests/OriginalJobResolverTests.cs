using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Providers;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class OriginalJobResolverTests
{
    private static OriginalJobResolver Build(params RawSearchResult[] results)
    {
        var provider = new StubProvider(results);
        var budget = new QueryBudgetManager(new DiscoveryBudgetOptions());
        return new OriginalJobResolver(new[] { (IRawSearchProvider)provider }, budget, NullLogger<OriginalJobResolver>.Instance);
    }

    private static RawJobCandidate Aggregator(string? company) =>
        new("Senior C# Developer", "https://jobgether.com/x", "SerperRaw", "jobgether.com",
            SourceType.Aggregator, 35, true, realCompanyName: company);

    [Fact]
    public async Task FindsOriginal_OnAts()
    {
        var resolver = Build(new RawSearchResult("C# Dev - MARGO", "https://boards.greenhouse.io/margo/jobs/9", null));
        var r = await resolver.ResolveAsync(Aggregator("MARGO"), CancellationToken.None);
        Assert.True(r.Found);
        Assert.Equal("Greenhouse", r.AtsProvider);
        Assert.Contains("greenhouse", r.OriginalUrl);
    }

    [Fact]
    public async Task NotFound_WhenNoAtsOrDomainHit_KeepsAsAggregator()
    {
        var resolver = Build(new RawSearchResult("random", "https://indeed.com/y", null));
        var r = await resolver.ResolveAsync(Aggregator("MARGO"), CancellationToken.None);
        Assert.False(r.Found);
        Assert.Contains("não encontrado", r.Reason);
    }

    [Fact]
    public async Task NoCompany_CannotResolve()
    {
        var resolver = Build();
        var r = await resolver.ResolveAsync(Aggregator(null), CancellationToken.None);
        Assert.False(r.Found);
    }

    [Fact]
    public async Task OfficialAtsCandidate_ResolvesImmediately()
    {
        var resolver = Build();
        var ats = new RawJobCandidate("Dev .NET", "https://acme.gupy.io/jobs/1", "SerperRaw", "acme.gupy.io",
            SourceType.OfficialAts, 80, false, realCompanyName: "Acme");
        var r = await resolver.ResolveAsync(ats, CancellationToken.None);
        Assert.True(r.Found);
        Assert.Equal(100, r.Confidence);
    }

    private sealed class StubProvider : IRawSearchProvider
    {
        private readonly IReadOnlyList<RawSearchResult> _r;
        public StubProvider(IReadOnlyList<RawSearchResult> r) => _r = r;
        public string ProviderName => "SerperRaw";
        public bool IsAvailable => true;
        public Task<IReadOnlyList<RawSearchResult>> SearchAsync(string q, int max, CancellationToken ct) => Task.FromResult(_r);
    }
}
