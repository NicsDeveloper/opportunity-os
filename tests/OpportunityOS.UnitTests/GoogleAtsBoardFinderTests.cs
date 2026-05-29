using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Providers;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class GoogleAtsBoardFinderTests
{
    private static Company Acme() =>
        new("Acme Pay", null, null, null, null, "Brazil", CompanyPriority.High, CompanySource.Bacen, null);

    private static HeuristicAtsBoardFinder DummyFallback() =>
        new(new FakeWeb(null), new FakeAts(new AtsDetectionResult(false, null, null, null, null, false)));

    [Fact]
    public async Task Configured_ClassifiesAtsBoardFromCseResult()
    {
        // CSE scoped to ATS domains returns the company's Gupy board.
        const string json = """
        { "items": [ { "link": "https://acmepay.gupy.io/" } ] }
        """;
        var finder = new GoogleAtsBoardFinder(
            new HttpClient(FakeHttpMessageHandler.Json(json)),
            new GoogleSearchOptions { ApiKey = "k", SearchEngineId = "cx" },
            DummyFallback(), NullLogger<GoogleAtsBoardFinder>.Instance);

        var r = await finder.FindAsync(Acme(), CancellationToken.None);

        Assert.True(r.Detected);
        Assert.Equal("Gupy", r.Ats);
        Assert.True(r.ProviderSupported);
        Assert.Equal("https://acmepay.gupy.io", r.BoardUrl);
    }

    [Fact]
    public async Task NotConfigured_DelegatesToFallback()
    {
        var fallback = new HeuristicAtsBoardFinder(
            new FakeWeb(null),
            new FakeAts(new AtsDetectionResult(true, "Lever", "https://jobs.lever.co/acme", "acme", null, true)));
        // Give the company a website so the fallback's detector path is reached.
        var company = new Company("Acme", "https://acme.com", null, null, null, "Brazil",
            CompanyPriority.High, CompanySource.Bacen, null);

        var finder = new GoogleAtsBoardFinder(
            new HttpClient(FakeHttpMessageHandler.Json("{}")),
            new GoogleSearchOptions(), // not configured
            fallback, NullLogger<GoogleAtsBoardFinder>.Instance);

        var r = await finder.FindAsync(company, CancellationToken.None);

        Assert.Equal("Lever", r.Ats);
    }

    private sealed class FakeWeb(string? url) : ICompanyWebsiteDiscoverer
    {
        public Task<WebsiteDiscoveryResult> DiscoverAsync(string companyName, CancellationToken ct) =>
            Task.FromResult(new WebsiteDiscoveryResult(url is not null, url));
    }

    private sealed class FakeAts(AtsDetectionResult result) : IAtsDetector
    {
        public Task<AtsDetectionResult> DetectAsync(Company company, CancellationToken ct) => Task.FromResult(result);
    }
}
