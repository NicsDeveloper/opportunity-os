using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.UnitTests.Fakes;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class JobDiscoveryServiceTests
{
    private static Company Acme()
    {
        var c = new Company("Acme", null, "https://boards.greenhouse.io/acme", null, null, "Brazil",
            CompanyPriority.High, CompanySource.Manual);
        return c;
    }

    private static JobDiscoveryService Build(FakeDiscoveryStore store, params IJobSourceProvider[] providers) =>
        new(providers, Array.Empty<IJobSearchProvider>(), store, NullLogger<JobDiscoveryService>.Instance);

    [Fact]
    public async Task Discover_CreatesNewJob()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(Acme());
        var provider = new FakeJobSourceProvider("Greenhouse",
            _ => new[] { FakeJobSourceProvider.Job("1", "Greenhouse") });

        var result = await Build(store, provider).DiscoverAsync(null, CancellationToken.None);

        Assert.Equal(1, result.JobsDiscovered);
        Assert.Equal(0, result.JobsUpdated);
        Assert.Single(store.Jobs);
        Assert.Single(store.Runs);
        Assert.Equal(ExecutionRunStatus.Succeeded.ToString(), result.Status);
    }

    [Fact]
    public async Task Discover_RediscoveredJob_IsUpdated_NotDuplicated()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(Acme());
        var provider = new FakeJobSourceProvider("Greenhouse",
            _ => new[] { FakeJobSourceProvider.Job("1", "Greenhouse", "Updated Title") });

        var service = Build(store, provider);
        await service.DiscoverAsync(null, CancellationToken.None);     // first time -> create
        var second = await service.DiscoverAsync(null, CancellationToken.None); // again -> update

        Assert.Equal(0, second.JobsDiscovered);
        Assert.Equal(1, second.JobsUpdated);
        Assert.Single(store.Jobs); // no duplicate
        Assert.Equal("Updated Title", store.Jobs[0].Title);
    }

    [Fact]
    public async Task Discover_ProviderFailure_IsRecorded_AndOtherProvidersContinue()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(Acme());
        var failing = new FakeJobSourceProvider("Failing",
            _ => throw new HttpRequestException("boom"));
        var working = new FakeJobSourceProvider("Working",
            _ => new[] { FakeJobSourceProvider.Job("ok-1", "Working") });

        var result = await Build(store, failing, working).DiscoverAsync(null, CancellationToken.None);

        Assert.Equal(1, result.Errors);
        Assert.Equal(1, result.JobsDiscovered);
        Assert.Single(store.Jobs);
        Assert.Equal(ExecutionRunStatus.PartiallyFailed.ToString(), result.Status);
    }

    [Fact]
    public async Task Discover_IntraRunDuplicate_IsIgnored()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(Acme());
        var provider = new FakeJobSourceProvider("Greenhouse", _ => new[]
        {
            FakeJobSourceProvider.Job("dup", "Greenhouse"),
            FakeJobSourceProvider.Job("dup", "Greenhouse")
        });

        var result = await Build(store, provider).DiscoverAsync(null, CancellationToken.None);

        Assert.Equal(1, result.JobsDiscovered);
        Assert.Single(store.Jobs);
    }

    [Fact]
    public async Task Search_AutoCreatesCompany_AndPersistsJob()
    {
        var store = new FakeDiscoveryStore(); // no companies registered
        var search = new FakeSearchProvider("Gupy", new[]
        {
            FakeJobSourceProvider.Job("g-1", "Gupy", "Desenvolvedor .NET") with { CompanyName = "Lumini IT" }
        });
        var service = new JobDiscoveryService(
            Array.Empty<IJobSourceProvider>(), new[] { search }, store, NullLogger<JobDiscoveryService>.Instance);

        var result = await service.SearchAsync(new[] { ".net", "c#" }, CancellationToken.None);

        Assert.Equal(1, result.JobsDiscovered);
        Assert.Single(store.Jobs);
        Assert.Single(store.Companies); // company auto-created from the result
        Assert.Equal("Lumini IT", store.Companies[0].Name);
    }

    private sealed class FakeSearchProvider : IJobSearchProvider
    {
        private readonly IReadOnlyCollection<DiscoveredJobDto> _jobs;
        public FakeSearchProvider(string name, IReadOnlyCollection<DiscoveredJobDto> jobs) { ProviderName = name; _jobs = jobs; }
        public string ProviderName { get; }
        public Task<IReadOnlyCollection<DiscoveredJobDto>> SearchAsync(IReadOnlyCollection<string> keywords, CancellationToken ct) =>
            Task.FromResult(_jobs);
    }

    [Fact]
    public async Task Discover_MarksCompanyScanned()
    {
        var store = new FakeDiscoveryStore();
        store.Companies.Add(Acme());
        var provider = new FakeJobSourceProvider("Greenhouse", _ => Array.Empty<DiscoveredJobDto>());

        await Build(store, provider).DiscoverAsync(null, CancellationToken.None);

        Assert.NotNull(store.Companies[0].LastScannedAtUtc);
    }
}
