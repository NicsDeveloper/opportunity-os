using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Providers;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class ProviderTests
{
    private static Company CompanyWith(string? careersUrl) =>
        new("Acme", "https://acme.example", careersUrl, null, null, "Brazil",
            CompanyPriority.High, CompanySource.Manual);

    [Fact]
    public async Task Greenhouse_ParsesJobs_AndDecodesHtmlContent()
    {
        const string json = """
        {
          "jobs": [
            {
              "id": 123,
              "title": "Senior Backend Engineer",
              "updated_at": "2025-01-02T10:00:00Z",
              "absolute_url": "https://boards.greenhouse.io/acme/jobs/123",
              "location": { "name": "Remote - Brazil" },
              "content": "&lt;p&gt;Build &lt;strong&gt;payments&lt;/strong&gt; with .NET&lt;/p&gt;",
              "departments": [ { "id": 1, "name": "Engineering" } ]
            }
          ]
        }
        """;
        var http = new HttpClient(FakeHttpMessageHandler.Json(json));
        var provider = new GreenhouseJobSourceProvider(http, NullLogger<GreenhouseJobSourceProvider>.Instance);
        var company = CompanyWith("https://boards.greenhouse.io/acme");

        Assert.True(provider.CanHandle(company));
        var jobs = await provider.DiscoverJobsAsync(company, CancellationToken.None);

        var job = Assert.Single(jobs);
        Assert.Equal("123", job.ExternalId);
        Assert.Equal("Senior Backend Engineer", job.Title);
        Assert.Equal("Remote - Brazil", job.Location);
        Assert.Equal("Engineering", job.Department);
        Assert.Equal("Greenhouse", job.SourceProvider);
        Assert.Equal("https://boards.greenhouse.io/acme/jobs/123", job.AbsoluteUrl);
        Assert.Equal("Build payments with .NET", job.DescriptionText);
        Assert.NotNull(job.UpdatedAtUtc);
    }

    [Fact]
    public void Greenhouse_CannotHandle_NonGreenhouseUrl()
    {
        var http = new HttpClient(FakeHttpMessageHandler.Json("{}"));
        var provider = new GreenhouseJobSourceProvider(http, NullLogger<GreenhouseJobSourceProvider>.Instance);
        Assert.False(provider.CanHandle(CompanyWith("https://jobs.lever.co/acme")));
    }

    [Fact]
    public async Task Lever_ParsesJobs_FromArray()
    {
        const string json = """
        [
          {
            "id": "abc-1",
            "text": "Backend Engineer",
            "categories": { "location": "Remote", "team": "Platform", "commitment": "Full-time" },
            "hostedUrl": "https://jobs.lever.co/acme/abc-1",
            "description": "<div>Build APIs</div>",
            "descriptionPlain": "Build APIs with .NET",
            "createdAt": 1735819200000
          }
        ]
        """;
        var http = new HttpClient(FakeHttpMessageHandler.Json(json));
        var provider = new LeverJobSourceProvider(http, NullLogger<LeverJobSourceProvider>.Instance);
        var company = CompanyWith("https://jobs.lever.co/acme");

        Assert.True(provider.CanHandle(company));
        var jobs = await provider.DiscoverJobsAsync(company, CancellationToken.None);

        var job = Assert.Single(jobs);
        Assert.Equal("abc-1", job.ExternalId);
        Assert.Equal("Backend Engineer", job.Title);
        Assert.Equal("Remote", job.Location);
        Assert.Equal("Platform", job.Department);
        Assert.Equal("Lever", job.SourceProvider);
        Assert.Equal("Build APIs with .NET", job.DescriptionText);
        Assert.NotNull(job.PublishedAtUtc);
    }

    [Fact]
    public async Task SmartRecruiters_ParsesListAndDetail()
    {
        const string list = """
        { "offset":0,"limit":100,"totalFound":1,"content":[
          { "id":"744000122509268","name":"Senior Backend Engineer (.NET)",
            "company":{"identifier":"acme","name":"Acme"},
            "releasedDate":"2026-04-23T16:54:54.835Z",
            "location":{"city":"Austin","region":"TX","country":"us","remote":true,"fullLocation":"Austin, TX"},
            "department":{"label":"Engineering"} } ] }
        """;
        const string detail = """
        { "applyUrl":"https://jobs.smartrecruiters.com/acme/744000122509268",
          "postingUrl":"https://jobs.smartrecruiters.com/acme/744000122509268-senior-backend",
          "jobAd":{"sections":{
            "jobDescription":{"title":"Job","text":"<p>Build payment APIs with .NET and C#</p>"},
            "qualifications":{"title":"Q","text":"<p>Kafka, AWS</p>"} }} }
        """;
        // List path ends with /postings; detail path has /postings/{id}.
        var handler = new FakeHttpMessageHandler(req =>
            req.RequestUri!.AbsolutePath.Contains("/postings/")
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(detail) }
                : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(list) });

        var provider = new SmartRecruitersJobSourceProvider(new HttpClient(handler), NullLogger<SmartRecruitersJobSourceProvider>.Instance);
        var company = new Company("Acme", null, "https://jobs.smartrecruiters.com/acme", null, null, "US",
            CompanyPriority.High, CompanySource.Manual);

        Assert.True(provider.CanHandle(company));
        var jobs = await provider.DiscoverJobsAsync(company, CancellationToken.None);

        var job = Assert.Single(jobs);
        Assert.Equal("744000122509268", job.ExternalId);
        Assert.Equal("Senior Backend Engineer (.NET)", job.Title);
        Assert.Equal("SmartRecruiters", job.SourceProvider);
        Assert.Contains("payment APIs with .NET", job.DescriptionText);
        Assert.Contains("Kafka", job.DescriptionText);
        Assert.Contains("smartrecruiters.com/acme/744000122509268", job.AbsoluteUrl);
        Assert.NotNull(job.PublishedAtUtc);
    }

    [Fact]
    public void SmartRecruiters_CannotHandle_NonSmartRecruitersUrl()
    {
        var provider = new SmartRecruitersJobSourceProvider(new HttpClient(FakeHttpMessageHandler.Json("{}")), NullLogger<SmartRecruitersJobSourceProvider>.Instance);
        var company = new Company("X", null, "https://boards.greenhouse.io/x", null, null, "US",
            CompanyPriority.Low, CompanySource.Manual);
        Assert.False(provider.CanHandle(company));
    }

    [Fact]
    public async Task Gupy_ParsesPortalJobs_AndDedupesById()
    {
        const string json = """
        {
          "data": [
            {
              "id": 9559956,
              "name": "Desenvolvedor .NET Sr. | AWS | Cliente Bancário",
              "description": "Vaga .NET / C# para cliente bancário, AWS, remoto.",
              "careerPageName": "Lumini IT Solutions",
              "publishedDate": "2025-07-22T19:57:14.484Z",
              "isRemoteWork": true,
              "country": "Brasil",
              "workplaceType": "remote",
              "jobUrl": "https://luminiitsolutions.gupy.io/job/abc"
            }
          ],
          "pagination": { "offset": 0, "limit": 50, "total": 1 }
        }
        """;
        var http = new HttpClient(FakeHttpMessageHandler.Json(json));
        var provider = new GupyJobSearchProvider(http, NullLogger<GupyJobSearchProvider>.Instance);

        // Two keywords hit the same job id -> deduped to one.
        var jobs = await provider.SearchAsync(new[] { ".net", "c#" }, CancellationToken.None);

        var job = Assert.Single(jobs);
        Assert.Equal("9559956", job.ExternalId);
        Assert.Equal("Lumini IT Solutions", job.CompanyName);
        Assert.Equal("Gupy", job.SourceProvider);
        Assert.Equal("Remoto - Brasil", job.Location);
        Assert.Equal("pt-BR", job.Language);
        Assert.NotNull(job.PublishedAtUtc);
    }
}
