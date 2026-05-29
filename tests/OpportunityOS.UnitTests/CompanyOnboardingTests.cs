using System.Net;
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
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<title>BTG Pactual — Banco</title>") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var discoverer = new CompanyWebsiteDiscoverer(new HttpClient(handler), NullLogger<CompanyWebsiteDiscoverer>.Instance);

        var r = await discoverer.DiscoverAsync("BANCO BTG PACTUAL S.A.", CancellationToken.None);

        Assert.True(r.Found);
        Assert.Contains("btgpactual", r.WebsiteUrl);
    }

    // ---- Heuristic board finder ----

    [Fact]
    public async Task Heuristic_DiscoversWebsite_ThenDetectsAts()
    {
        var finder = new HeuristicAtsBoardFinder(
            new FakeWebsiteDiscoverer("https://www.acmepay.com.br"),
            new FakeAtsDetector(new AtsDetectionResult(true, "Greenhouse", "https://boards.greenhouse.io/acmepay", "acmepay", null, true)));
        var company = new Company("Acme Pay", null, null, null, null, "Brazil", CompanyPriority.High, CompanySource.Bacen, null);

        var r = await finder.FindAsync(company, CancellationToken.None);

        Assert.True(r.Detected);
        Assert.Equal("https://www.acmepay.com.br", company.WebsiteUrl); // discovered + set
    }

    // ---- Onboarding chain ----

    [Fact]
    public async Task Onboard_FindsBoard_AndPersistsCareersUrlAndTag()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(new Company("Acme Pay", null, null, null, null, "Brazil",
            CompanyPriority.High, CompanySource.Bacen, new[] { "bacen" }));

        var service = new CompanyOnboardingService(
            store,
            new FakeBoardFinder(new AtsDetectionResult(true, "Greenhouse", "https://boards.greenhouse.io/acmepay", "acmepay", null, true)),
            NullLogger<CompanyOnboardingService>.Instance);

        var result = await service.OnboardAsync(25, CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.BoardsFound);
        Assert.Equal("https://boards.greenhouse.io/acmepay", store.Companies[0].CareersUrl);
        Assert.Contains("greenhouse", store.Companies[0].Tags);
    }

    [Fact]
    public async Task Onboard_SkipsCompaniesThatAlreadyHaveBoard()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(new Company("Has Board", "https://x.com", "https://boards.greenhouse.io/x", null, null,
            "Brazil", CompanyPriority.High, CompanySource.Manual, null));

        var service = new CompanyOnboardingService(
            store, new FakeBoardFinder(new AtsDetectionResult(true, "Lever", "https://jobs.lever.co/should-not", null, null, true)),
            NullLogger<CompanyOnboardingService>.Instance);

        var result = await service.OnboardAsync(25, CancellationToken.None);

        Assert.Equal(0, result.Processed);
        Assert.Equal("https://boards.greenhouse.io/x", store.Companies[0].CareersUrl);
    }

    private sealed class FakeWebsiteDiscoverer(string? url) : ICompanyWebsiteDiscoverer
    {
        public Task<WebsiteDiscoveryResult> DiscoverAsync(string companyName, CancellationToken ct) =>
            Task.FromResult(new WebsiteDiscoveryResult(url is not null, url));
    }

    private sealed class FakeAtsDetector(AtsDetectionResult result) : IAtsDetector
    {
        public Task<AtsDetectionResult> DetectAsync(Company company, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class FakeBoardFinder(AtsDetectionResult result) : IAtsBoardFinder
    {
        public Task<AtsDetectionResult> FindAsync(Company company, CancellationToken ct) => Task.FromResult(result);
    }
}
