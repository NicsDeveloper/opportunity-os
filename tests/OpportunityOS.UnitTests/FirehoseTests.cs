using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class FirehoseTests
{
    // ---------- Query expansion ----------

    [Fact]
    public void ExpandAggressive_GeneratesAtLeast100Queries()
    {
        var svc = new QueryExpansionService();
        var queries = svc.ExpandAggressive(Array.Empty<string>(), maxQueries: 1000);
        Assert.True(queries.Count >= 100, $"expected >= 100 queries, got {queries.Count}");
        Assert.Equal(queries.Count, queries.Distinct().Count()); // no dupes
    }

    [Fact]
    public void ExpandAggressive_RespectsMaxQueries()
    {
        var svc = new QueryExpansionService();
        var queries = svc.ExpandAggressive(new[] { "vaga net" }, maxQueries: 50);
        Assert.Equal(50, queries.Count);
    }

    [Fact]
    public void ExpandForCompany_ProducesTargetedQueries()
    {
        var svc = new QueryExpansionService();
        var queries = svc.ExpandForCompany("Itaú", maxQueries: 100);
        Assert.NotEmpty(queries);
        Assert.All(queries, q => Assert.Contains("Itaú", q));
        Assert.Contains(queries, q => q.Contains(".NET"));
    }

    // ---------- Firehose service ----------

    [Fact]
    public async Task QuickSearch_SavesRawCandidates_AndRecordsAudit()
    {
        var provider = new FakeRawProvider(new[]
        {
            new RawSearchResult("Dev .NET", "https://acme.gupy.io/jobs/1", "snippet"),
            new RawSearchResult("C# Backend", "https://boards.greenhouse.io/acme/jobs/2", "snippet"),
        });
        var store = new FakeFirehoseStore();
        var svc = Build(store, provider);

        var r = await svc.QuickSearchAsync(new QuickSearchRequest("vaga .net", 20, true), CancellationToken.None);

        Assert.Equal(2, store.RawCandidates.Count);
        Assert.Equal(2, r.NewCandidates);
        Assert.Equal(0, r.Duplicates);
        Assert.Single(store.Runs);                  // one ExecutionRun
        Assert.Single(store.QueryExecutions);       // one query execution
        Assert.Equal("Succeeded", r.Status);
    }

    [Fact]
    public async Task QuickSearch_DedupesByUrl()
    {
        var provider = new FakeRawProvider(new[]
        {
            new RawSearchResult("Dev .NET", "https://acme.gupy.io/jobs/1", null),
            new RawSearchResult("Dev .NET dup", "https://acme.gupy.io/jobs/1", null),
        });
        var store = new FakeFirehoseStore();
        var svc = Build(store, provider);

        var r = await svc.QuickSearchAsync(new QuickSearchRequest("vaga", 20, true), CancellationToken.None);

        Assert.Single(store.RawCandidates);   // dup not saved
        Assert.Equal(1, r.NewCandidates);
        Assert.Equal(1, r.Duplicates);
    }

    [Fact]
    public async Task QuickSearch_ClassifiesSocialAsManualReview()
    {
        var provider = new FakeRawProvider(new[]
        {
            new RawSearchResult("Dev .NET", "https://br.linkedin.com/jobs/view/123", null),
        });
        var store = new FakeFirehoseStore();
        var svc = Build(store, provider);

        await svc.QuickSearchAsync(new QuickSearchRequest("vaga", 20, true), CancellationToken.None);

        var c = Assert.Single(store.RawCandidates);
        Assert.Equal("SocialIndexed", c.SourceType.ToString());
        Assert.True(c.RequiresManualValidation);
    }

    [Fact]
    public async Task AggressiveSearch_RunsManyQueries_AndSavesCandidates()
    {
        var provider = new FakeRawProvider(new[]
        {
            new RawSearchResult("Dev .NET", "https://x/" , null), // url will be made unique per query below
        });
        var store = new FakeFirehoseStore();
        var svc = Build(store, provider);

        var r = await svc.AggressiveSearchAsync(
            new AggressiveSearchRequest(null, 120, 20, true, false), CancellationToken.None);

        Assert.True(r.QueriesExecuted >= 100, $"expected >= 100, got {r.QueriesExecuted}");
        Assert.True(store.QueryExecutions.Count >= 100);
    }

    [Fact]
    public async Task QuickSearch_FlagsSemanticDuplicate_AcrossDifferentUrls()
    {
        // Same vacancy (same title -> same company "Acme"), two different URLs.
        var provider = new FixedRawProvider(new[]
        {
            new RawSearchResult("Senior .NET Developer at Acme", "https://site-a.com/p1", null),
            new RawSearchResult("Senior .NET Developer at Acme", "https://site-b.com/p2", null),
        });
        var store = new FakeFirehoseStore();
        var svc = Build(store, provider);

        var r = await svc.QuickSearchAsync(new QuickSearchRequest("vaga", 20, true), CancellationToken.None);

        Assert.Equal(2, store.RawCandidates.Count);  // both kept (visible as occurrences)
        Assert.Equal(1, r.NewCandidates);
        Assert.Equal(1, r.Duplicates);
        Assert.Contains(store.RawCandidates, c => c.Status.ToString() == "Duplicate");
    }

    private sealed class FixedRawProvider : IRawSearchProvider
    {
        private readonly IReadOnlyList<RawSearchResult> _results;
        public FixedRawProvider(IReadOnlyList<RawSearchResult> results) => _results = results;
        public string ProviderName => "FixedRaw";
        public bool IsAvailable => true;
        public Task<IReadOnlyList<RawSearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct) =>
            Task.FromResult(_results);
    }

    private static FirehoseService Build(FakeFirehoseStore store, params IRawSearchProvider[] providers) =>
        BuildWith(store, new FakeBudget(int.MaxValue), new DiscoveryBudgetOptions(), providers);

    private static FirehoseService BuildWith(
        FakeFirehoseStore store, IQueryBudgetManager budget, DiscoveryBudgetOptions opts, params IRawSearchProvider[] providers) =>
        new(providers, new QueryExpansionService(), store, budget, opts,
            new OpportunityOS.Infrastructure.Providers.SourceClassifierService(),
            new OpportunityOS.Infrastructure.Providers.CompanyNameResolver(),
            new JobFingerprintService(), NullLogger<FirehoseService>.Instance);

    [Fact]
    public async Task AggressiveSearch_StopsWithBudgetLimit_WhenBudgetExhausted()
    {
        var provider = new FakeRawProvider(new[] { new RawSearchResult("Dev", "https://x/", null) });
        var store = new FakeFirehoseStore();
        // Budget = 0 -> provider can never execute -> clean stop, no queries run.
        var svc = BuildWith(store, new FakeBudget(0), new DiscoveryBudgetOptions(), provider);

        var r = await svc.AggressiveSearchAsync(
            new AggressiveSearchRequest(null, 100, 10, true, false), CancellationToken.None);

        Assert.Equal("CompletedWithBudgetLimit", r.Status);
        Assert.Equal(0, r.NewCandidates);
        Assert.Empty(store.RawCandidates);
    }

    // ---------- fakes ----------

    private sealed class FakeRawProvider : IRawSearchProvider
    {
        private readonly IReadOnlyList<RawSearchResult> _results;
        private int _call;
        public FakeRawProvider(IReadOnlyList<RawSearchResult> results) => _results = results;
        public string ProviderName => "FakeRaw";
        public bool IsAvailable => true;
        public Task<IReadOnlyList<RawSearchResult>> SearchAsync(string query, int maxResults, CancellationToken ct)
        {
            // Make URLs unique per query so aggressive runs produce distinct candidates.
            var n = Interlocked.Increment(ref _call);
            var mapped = _results.Select((r, i) =>
                r with { Url = r.Url.Contains("jobs/1") || r.Url.Contains("linkedin") || r.Url.Contains("greenhouse")
                    ? r.Url : $"{r.Url}{n}-{i}" }).ToList();
            return Task.FromResult<IReadOnlyList<RawSearchResult>>(mapped);
        }
    }

    private sealed class FakeBudget : IQueryBudgetManager
    {
        private readonly int _limit;
        private int _used;
        public FakeBudget(int limit) => _limit = limit;
        public Task<bool> CanExecuteAsync(string provider, CancellationToken ct) => Task.FromResult(_used < _limit);
        public Task RecordExecutionAsync(string provider, int cost, CancellationToken ct) { _used += cost; return Task.CompletedTask; }
        public BudgetSnapshot Snapshot(string provider) => new(provider, _used, _limit);
    }

    private sealed class FakeFirehoseStore : IFirehoseStore
    {
        public List<SearchCampaign> Campaigns { get; } = new();
        public List<RawJobCandidate> RawCandidates { get; } = new();
        public List<SearchQueryExecution> QueryExecutions { get; } = new();
        public List<ExecutionRun> Runs { get; } = new();

        public Task AddCampaignAsync(SearchCampaign c, CancellationToken ct) { Campaigns.Add(c); return Task.CompletedTask; }
        public Task<SearchCampaign?> GetCampaignAsync(Guid id, CancellationToken ct) => Task.FromResult(Campaigns.FirstOrDefault(c => c.Id == id));
        public Task<IReadOnlyList<SearchCampaign>> GetCampaignsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SearchCampaign>>(Campaigns);
        public Task<bool> RawCandidateExistsByUrlAsync(string url, CancellationToken ct) => Task.FromResult(RawCandidates.Any(c => c.DiscoveredUrl == url));
        public Task<bool> RawCandidateExistsByFingerprintAsync(string fp, CancellationToken ct) => Task.FromResult(RawCandidates.Any(c => c.NormalizedFingerprint == fp));
        public Task AddRawCandidateAsync(RawJobCandidate c, CancellationToken ct) { RawCandidates.Add(c); return Task.CompletedTask; }
        public Task AddQueryExecutionAsync(SearchQueryExecution e, CancellationToken ct) { QueryExecutions.Add(e); return Task.CompletedTask; }
        public Task AddExecutionRunAsync(ExecutionRun r, CancellationToken ct) { Runs.Add(r); return Task.CompletedTask; }
        public Task<IReadOnlyList<RawJobCandidate>> GetRawCandidatesAsync(int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<RawJobCandidate>>(RawCandidates.Take(take).ToList());
        public Task<int> CountRawCandidatesAsync(DateTime? since, CancellationToken ct) => Task.FromResult(RawCandidates.Count);
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
