using OpportunityOS.Application.Discovery;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.UnitTests.Fakes;

/// <summary>In-memory <see cref="IDiscoveryStore"/> for orchestration tests.</summary>
public sealed class FakeDiscoveryStore : IDiscoveryStore
{
    public List<Company> Companies { get; } = new();
    public List<JobPosting> Jobs { get; } = new();
    public List<ExecutionRun> Runs { get; } = new();
    public List<OpportunityMatch> Matches { get; } = new();
    public CandidateProfile? ActiveProfile { get; set; }
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Company>> GetCompaniesByPriorityAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Company>>(Companies);

    public Task<Company?> GetCompanyAsync(Guid companyId, CancellationToken ct) =>
        Task.FromResult(Companies.FirstOrDefault(c => c.Id == companyId));

    public Task<JobPosting?> FindJobAsync(string sourceProvider, string externalId, CancellationToken ct) =>
        Task.FromResult(Jobs.FirstOrDefault(j => j.SourceProvider == sourceProvider && j.ExternalId == externalId));

    public Task AddJobAsync(JobPosting job, CancellationToken ct)
    {
        Jobs.Add(job);
        return Task.CompletedTask;
    }

    public Task<Company> FindOrCreateCompanyByNameAsync(string name, CancellationToken ct)
    {
        var existing = Companies.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new Company(name, null, null, null, null, "Brazil",
                CompanyPriority.Medium, CompanySource.AtsDiscovery, new[] { "gupy" });
            Companies.Add(existing);
        }
        return Task.FromResult(existing);
    }

    public Task AddExecutionRunAsync(ExecutionRun run, CancellationToken ct)
    {
        Runs.Add(run);
        return Task.CompletedTask;
    }

    public Task<CandidateProfile?> GetActiveProfileAsync(CancellationToken ct) =>
        Task.FromResult(ActiveProfile);

    public Task<bool> JobHasMatchAsync(Guid jobPostingId, CancellationToken ct) =>
        Task.FromResult(Matches.Any(m => m.JobPostingId == jobPostingId));

    public Task AddMatchAsync(OpportunityMatch match, CancellationToken ct)
    {
        Matches.Add(match);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Configurable provider: returns canned jobs or throws.</summary>
public sealed class FakeJobSourceProvider : IJobSourceProvider
{
    private readonly Func<Company, IReadOnlyCollection<DiscoveredJobDto>> _factory;

    public FakeJobSourceProvider(string name, Func<Company, IReadOnlyCollection<DiscoveredJobDto>> factory)
    {
        ProviderName = name;
        _factory = factory;
    }

    public string ProviderName { get; }
    public bool CanHandle(Company company) => true;

    public Task<IReadOnlyCollection<DiscoveredJobDto>> DiscoverJobsAsync(Company company, CancellationToken ct) =>
        Task.FromResult(_factory(company));

    public static DiscoveredJobDto Job(string externalId, string provider, string title = "Backend Engineer") =>
        new(externalId, title, "Acme", "Remote", "Engineering", null, "desc",
            $"https://example.com/{externalId}", provider, null, DateTime.UtcNow, "en");
}
