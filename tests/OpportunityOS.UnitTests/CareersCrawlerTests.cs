using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Providers;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class CareersCrawlerTests
{
    private static Company Acme() =>
        new("Acme", "https://www.acme.com.br", null, null, null, "Brazil",
            CompanyPriority.High, CompanySource.Bacen, null);

    [Fact]
    public async Task Crawl_FindsCareersPage_ThenExtractsRoleJobLinks()
    {
        const string home = """
        <html><body>
          <a href="/carreiras">Trabalhe Conosco</a>
          <a href="/sobre">Sobre</a>
        </body></html>
        """;
        const string careers = """
        <html><body>
          <a href="https://www.acme.com.br/vagas/desenvolvedor-net-senior">Desenvolvedor .NET Sênior</a>
          <a href="/vagas/analista-rh">Analista de RH</a>
          <a href="https://www.acme.com.br/vagas/backend-engineer">Backend Engineer</a>
          <a href="https://boards.greenhouse.io/other/jobs/1">Externo Greenhouse</a>
        </body></html>
        """;
        var handler = new FakeHttpMessageHandler(req =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(req.RequestUri!.AbsolutePath.Contains("carreiras") ? careers : home),
            });
        var crawler = new GenericCareersCrawler(new HttpClient(handler), NullLogger<GenericCareersCrawler>.Instance);

        Assert.True(crawler.CanHandle(Acme()));
        var jobs = await crawler.DiscoverJobsAsync(Acme(), CancellationToken.None);

        var titles = jobs.Select(j => j.Title).ToList();
        Assert.Contains("Desenvolvedor .NET Sênior", titles);
        Assert.Contains("Backend Engineer", titles);
        Assert.DoesNotContain("Analista de RH", titles);          // not a tech/role match
        Assert.DoesNotContain(jobs, j => j.AbsoluteUrl.Contains("greenhouse.io")); // ATS handled elsewhere
        Assert.All(jobs, j => Assert.Equal("CareersCrawler", j.SourceProvider));
    }

    [Fact]
    public void CannotHandle_WithoutWebsite()
    {
        var crawler = new GenericCareersCrawler(new HttpClient(FakeHttpMessageHandler.Json("")), NullLogger<GenericCareersCrawler>.Instance);
        var noSite = new Company("X", null, null, null, null, "BR", CompanyPriority.Low, CompanySource.Bacen, null);
        Assert.False(crawler.CanHandle(noSite));
    }
}
