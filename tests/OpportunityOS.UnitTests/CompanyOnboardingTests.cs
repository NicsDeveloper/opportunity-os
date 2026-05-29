using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Providers;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class CompanyOnboardingTests
{
    // ---- Name normalizer ----

    [Fact]
    public void BrandSlugs_StripsLegalSuffixAndLeadingBanco()
    {
        var slugs = CompanyNameNormalizer.BrandSlugs("BANCO BTG PACTUAL S.A.");
        Assert.Contains("btgpactual", slugs);
        Assert.Contains("bancobtgpactual", slugs);
    }

    [Fact]
    public void CandidateUrls_PrefersBrazilTld()
    {
        var urls = CompanyNameNormalizer.CandidateUrls("BANCO BTG PACTUAL S.A.");
        Assert.Equal("https://www.btgpactual.com.br", urls[0]);
    }

    // ---- Website discoverer (fake HTTP) ----

    [Fact]
    public async Task Discoverer_PicksCandidateThatReturns200AndMentionsBrand()
    {
        var handler = new FakeHttpMessageHandler(req =>
            req.RequestUri!.Host.Contains("btgpactual")
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("<title>BTG Pactual — Banco</title>")
                }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        var discoverer = new CompanyWebsiteDiscoverer(new HttpClient(handler), NullLogger<CompanyWebsiteDiscoverer>.Instance);

        var r = await discoverer.DiscoverAsync("BANCO BTG PACTUAL S.A.", CancellationToken.None);

        Assert.True(r.Found);
        Assert.Contains("btgpactual", r.WebsiteUrl);
    }

    [Fact]
    public async Task Discoverer_ReturnsNotFound_WhenNothingResponds()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        var discoverer = new CompanyWebsiteDiscoverer(new HttpClient(handler), NullLogger<CompanyWebsiteDiscoverer>.Instance);

        var r = await discoverer.DiscoverAsync("Empresa Inexistente XYZ", CancellationToken.None);
        Assert.False(r.Found);
    }

    // ---- Onboarding chain (fakes) ----

    [Fact]
    public async Task Onboard_DiscoversWebsite_ThenDetectsAts_AndPersists()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(new Company("Acme Pay", null, null, null, null, "Brazil",
            CompanyPriority.High, CompanySource.Bacen, new[] { "bacen" }));

        var service = new CompanyOnboardingService(
            store,
            new FakeWebsiteDiscoverer("https://www.acmepay.com.br"),
            new FakeAtsDetector(new AtsDetectionResult(true, "Greenhouse", "https://boards.greenhouse.io/acmepay", "acmepay", null, true)),
            NullLogger<CompanyOnboardingService>.Instance);

        var result = await service.OnboardAsync(25, CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.WebsitesFound);
        Assert.Equal(1, result.AtsDetected);
        var company = store.Companies[0];
        Assert.Equal("https://www.acmepay.com.br", company.WebsiteUrl);
        Assert.Equal("https://boards.greenhouse.io/acmepay", company.CareersUrl);
        Assert.Contains("greenhouse", company.Tags);
    }

    [Fact]
    public async Task Onboard_SkipsCompaniesThatAlreadyHaveWebsite()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(new Company("Has Site", "https://hassite.com", null, null, null, "Brazil",
            CompanyPriority.High, CompanySource.Manual, null));

        var service = new CompanyOnboardingService(
            store, new FakeWebsiteDiscoverer("https://should-not-be-used.com"),
            new FakeAtsDetector(new AtsDetectionResult(false, null, null, null, null, false)),
            NullLogger<CompanyOnboardingService>.Instance);

        var result = await service.OnboardAsync(25, CancellationToken.None);
        Assert.Equal(0, result.Processed);
        Assert.Equal("https://hassite.com", store.Companies[0].WebsiteUrl);
    }

    private sealed class FakeWebsiteDiscoverer(string? url) : ICompanyWebsiteDiscoverer
    {
        public Task<WebsiteDiscoveryResult> DiscoverAsync(string companyName, CancellationToken ct) =>
            Task.FromResult(new WebsiteDiscoveryResult(url is not null, url));
    }

    private sealed class FakeAtsDetector(AtsDetectionResult result) : IAtsDetector
    {
        public Task<AtsDetectionResult> DetectAsync(Company company, CancellationToken ct) =>
            Task.FromResult(result);
    }
}
