using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Application.Discovery;
using OpportunityOS.Application.Matching;
using OpportunityOS.Application.Normalization;
using OpportunityOS.Contracts;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Infrastructure.Persistence;
using OpportunityOS.Infrastructure.Providers;
using Xunit;

namespace OpportunityOS.UnitTests.Integration;

/// <summary>
/// End-to-end over the REAL EF stores (InMemory DB): Firehose collect -> persist ->
/// promote -> JobPosting + OpportunityMatch + SourceOccurrence. No Docker, no HTTP.
/// </summary>
public sealed class FirehosePipelineIntegrationTests
{
    private static OpportunityOsDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<OpportunityOsDbContext>()
            .UseInMemoryDatabase("itest-" + Guid.NewGuid()).Options;
        var db = new OpportunityOsDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    private static void SeedProfile(OpportunityOsDbContext db)
    {
        db.CandidateProfiles.Add(new CandidateProfile(
            "Test", "Backend .NET", "summary", "Remote", "Senior", "pt-BR",
            new[] { ".NET", "C#", "Backend" }, Array.Empty<string>(), new[] { "Pagamentos" },
            Array.Empty<string>(), Array.Empty<string>(), new[] { "Remote" }, new List<CandidateExperience>()));
        db.SaveChanges();
    }

    private sealed class FakeRaw : IRawSearchProvider
    {
        public string ProviderName => "SerperRaw";
        public bool IsAvailable => true;
        public Task<IReadOnlyList<RawSearchResult>> SearchAsync(string q, int max, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RawSearchResult>>(new[]
            {
                new RawSearchResult("Desenvolvedor .NET Sênior", "https://acme.gupy.io/jobs/1", "C# backend pagamentos"),
                new RawSearchResult("Engenheiro de Software C#", "https://boards.greenhouse.io/stone/jobs/2", "C# .NET microsserviços"),
            });
    }

    [Fact]
    public async Task Firehose_Collects_Then_Promote_CreatesJob_Match_AndOccurrence()
    {
        using var db = NewDb();
        SeedProfile(db);

        var normalizer = new JobNormalizer();
        var firehose = new FirehoseService(
            new[] { (IRawSearchProvider)new FakeRaw() }, new QueryExpansionService(),
            new EfFirehoseStore(db), new QueryBudgetManager(new DiscoveryBudgetOptions()),
            new DiscoveryBudgetOptions(), new SourceClassifierService(), new CompanyNameResolver(),
            new JobFingerprintService(), NullLogger<FirehoseService>.Instance);

        // 1) Firehose collects + classifies + resolves company + persists.
        var collected = await firehose.QuickSearchAsync(new QuickSearchRequest("vaga .net", 20, true), CancellationToken.None);
        Assert.Equal(2, collected.NewCandidates);
        var raws = await db.RawJobCandidates.ToListAsync();
        Assert.Equal(2, raws.Count);
        Assert.All(raws, r => Assert.False(string.IsNullOrWhiteSpace(r.RealCompanyName))); // company resolved from ATS host
        Assert.All(raws, r => Assert.Equal("OfficialAts", r.SourceType.ToString()));

        // 2) Promote -> JobPosting + Match + SourceOccurrence.
        var promo = new RawCandidatePromotionService(
            new EfFirehoseStore(db), new EfDiscoveryStore(db, new EfLatestOpportunityMatchProjection(db, NullLogger<EfLatestOpportunityMatchProjection>.Instance)), new JobFingerprintService(),
            normalizer, new HeuristicMatchEngine(normalizer), NullLogger<RawCandidatePromotionService>.Instance);
        var result = await promo.PromoteBatchAsync(new PromoteBatchRequest(50, 50, true), CancellationToken.None);

        Assert.Equal(2, result.Promoted);
        Assert.Equal(2, await db.JobPostings.CountAsync());
        Assert.Equal(2, await db.JobPostingSourceOccurrences.CountAsync());
        Assert.True(await db.OpportunityMatches.AnyAsync());                 // scored -> shows in /matches
        Assert.All(await db.JobPostings.ToListAsync(), j => Assert.False(string.IsNullOrWhiteSpace(j.NormalizedFingerprint)));
    }

    [Fact]
    public async Task Promote_IsIdempotent_AcrossRuns()
    {
        using var db = NewDb();
        SeedProfile(db);
        var normalizer = new JobNormalizer();
        IRawSearchProvider[] providers = { new FakeRaw() };
        var firehose = new FirehoseService(providers, new QueryExpansionService(), new EfFirehoseStore(db),
            new QueryBudgetManager(new DiscoveryBudgetOptions()), new DiscoveryBudgetOptions(),
            new SourceClassifierService(), new CompanyNameResolver(), new JobFingerprintService(),
            NullLogger<FirehoseService>.Instance);
        await firehose.QuickSearchAsync(new QuickSearchRequest("vaga", 20, true), CancellationToken.None);

        var promo = new RawCandidatePromotionService(new EfFirehoseStore(db), new EfDiscoveryStore(db, new EfLatestOpportunityMatchProjection(db, NullLogger<EfLatestOpportunityMatchProjection>.Instance)),
            new JobFingerprintService(), normalizer, new HeuristicMatchEngine(normalizer),
            NullLogger<RawCandidatePromotionService>.Instance);

        await promo.PromoteBatchAsync(new PromoteBatchRequest(50, 50, true), CancellationToken.None);
        var jobsAfterFirst = await db.JobPostings.CountAsync();
        await promo.PromoteBatchAsync(new PromoteBatchRequest(50, 50, true), CancellationToken.None);
        var jobsAfterSecond = await db.JobPostings.CountAsync();

        Assert.Equal(jobsAfterFirst, jobsAfterSecond); // already-promoted candidates not re-created
    }
}
