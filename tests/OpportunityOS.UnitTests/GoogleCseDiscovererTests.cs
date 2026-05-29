using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Infrastructure.Providers;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class GoogleCseDiscovererTests
{
    private static CompanyWebsiteDiscoverer Fallback(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<CompanyWebsiteDiscoverer>.Instance);

    [Fact]
    public async Task Configured_SkipsBlockedHosts_AndReturnsFirstOrganicRoot()
    {
        const string json = """
        { "items": [
            { "link": "https://br.linkedin.com/company/btg-pactual" },
            { "link": "https://www.btgpactual.com.br/para-voce/investimentos" }
        ]}
        """;
        var google = new GoogleCustomSearchWebsiteDiscoverer(
            new HttpClient(FakeHttpMessageHandler.Json(json)),
            new GoogleSearchOptions { ApiKey = "k", SearchEngineId = "cx" },
            Fallback(FakeHttpMessageHandler.Json("{}")),
            NullLogger<GoogleCustomSearchWebsiteDiscoverer>.Instance);

        var r = await google.DiscoverAsync("BANCO BTG PACTUAL S.A.", CancellationToken.None);

        Assert.True(r.Found);
        Assert.Equal("https://www.btgpactual.com.br", r.WebsiteUrl); // linkedin skipped; reduced to root
    }

    [Fact]
    public async Task NotConfigured_DelegatesToHeuristicFallback()
    {
        // Fallback returns 200 + brand mention for the btgpactual candidate domain.
        var fallbackHandler = new FakeHttpMessageHandler(req =>
            req.RequestUri!.Host.Contains("btgpactual")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("BTG Pactual") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var google = new GoogleCustomSearchWebsiteDiscoverer(
            new HttpClient(FakeHttpMessageHandler.Json("{}")),
            new GoogleSearchOptions(), // not configured
            Fallback(fallbackHandler),
            NullLogger<GoogleCustomSearchWebsiteDiscoverer>.Instance);

        var r = await google.DiscoverAsync("BANCO BTG PACTUAL S.A.", CancellationToken.None);

        Assert.True(r.Found);
        Assert.Contains("btgpactual", r.WebsiteUrl);
    }
}
