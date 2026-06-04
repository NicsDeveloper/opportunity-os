using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class LatestOpportunityMatchProjectionTests
{
    private static OpportunityOsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<OpportunityOsDbContext>()
            .UseInMemoryDatabase("lom-" + Guid.NewGuid()).Options);

    private static EfLatestOpportunityMatchProjection Proj(OpportunityOsDbContext db) =>
        new(db, NullLogger<EfLatestOpportunityMatchProjection>.Instance);

    private static readonly DateTime Base = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // OpportunityMatch.CreatedAtUtc is set to "now" in the ctor; force a deterministic value for ordering.
    private static OpportunityMatch Match(Guid job, Guid profile, int score, DateTime createdAt, string engine = "heuristic-v2")
    {
        var m = new OpportunityMatch(job, profile, score, score, 0, 0, 0, 0,
            MatchRecommendation.Apply, new[] { "s" }, Array.Empty<string>(), Array.Empty<string>(), "r", engine);
        typeof(OpportunityMatch).GetProperty(nameof(OpportunityMatch.CreatedAtUtc))!.SetValue(m, createdAt);
        return m;
    }

    private static async Task<OpportunityMatch> AddAndUpsert(
        OpportunityOsDbContext db, EfLatestOpportunityMatchProjection proj, OpportunityMatch m)
    {
        db.OpportunityMatches.Add(m);
        await proj.UpsertAsync(m, default);
        await db.SaveChangesAsync();
        return m;
    }

    [Fact]
    public async Task Upsert_CreatesProjection_WhenNoneExists()
    {
        using var db = NewDb();
        var (job, profile) = (Guid.NewGuid(), Guid.NewGuid());
        var m = await AddAndUpsert(db, Proj(db), Match(job, profile, 90, Base));

        var row = await db.LatestOpportunityMatches.SingleAsync();
        Assert.Equal(job, row.JobPostingId);
        Assert.Equal(profile, row.CandidateProfileId);
        Assert.Equal(m.Id, row.OpportunityMatchId);
        Assert.Equal(90, row.OverallScore);
    }

    [Fact]
    public async Task Upsert_UpdatesProjection_WhenNewerMatchArrives()
    {
        using var db = NewDb();
        var proj = Proj(db);
        var (job, profile) = (Guid.NewGuid(), Guid.NewGuid());
        await AddAndUpsert(db, proj, Match(job, profile, 90, Base));
        var newer = await AddAndUpsert(db, proj, Match(job, profile, 60, Base.AddMinutes(5)));

        var row = await db.LatestOpportunityMatches.SingleAsync();
        Assert.Equal(newer.Id, row.OpportunityMatchId);
        Assert.Equal(60, row.OverallScore);
    }

    [Fact]
    public async Task Upsert_IgnoresOlderMatch()
    {
        using var db = NewDb();
        var proj = Proj(db);
        var (job, profile) = (Guid.NewGuid(), Guid.NewGuid());
        var newer = await AddAndUpsert(db, proj, Match(job, profile, 90, Base.AddMinutes(5)));
        await AddAndUpsert(db, proj, Match(job, profile, 40, Base)); // older timestamp

        var row = await db.LatestOpportunityMatches.SingleAsync();
        Assert.Equal(newer.Id, row.OpportunityMatchId); // still the newer match
        Assert.Equal(90, row.OverallScore);
    }

    [Fact]
    public async Task Upsert_DoesNotMixProfiles()
    {
        using var db = NewDb();
        var proj = Proj(db);
        var job = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await AddAndUpsert(db, proj, Match(job, a, 90, Base));
        await AddAndUpsert(db, proj, Match(job, b, 30, Base));
        // Update A again — B must be untouched.
        await AddAndUpsert(db, proj, Match(job, a, 70, Base.AddMinutes(10)));

        var rows = await db.LatestOpportunityMatches.ToListAsync();
        Assert.Equal(2, rows.Count); // same job, two profiles -> two projections
        Assert.Equal(70, rows.Single(r => r.CandidateProfileId == a).OverallScore);
        Assert.Equal(30, rows.Single(r => r.CandidateProfileId == b).OverallScore);
    }

    [Fact]
    public async Task SameJob_TwoProfiles_ProduceTwoProjections()
    {
        using var db = NewDb();
        var proj = Proj(db);
        var job = Guid.NewGuid();
        await AddAndUpsert(db, proj, Match(job, Guid.NewGuid(), 92, Base));
        await AddAndUpsert(db, proj, Match(job, Guid.NewGuid(), 24, Base));

        Assert.Equal(2, await db.LatestOpportunityMatches.CountAsync());
        Assert.Single(await db.LatestOpportunityMatches.Where(r => r.JobPostingId == job && r.OverallScore == 92).ToListAsync());
        Assert.Single(await db.LatestOpportunityMatches.Where(r => r.JobPostingId == job && r.OverallScore == 24).ToListAsync());
    }

    [Fact]
    public async Task Backfill_BuildsLatestPerJobAndProfile()
    {
        using var db = NewDb();
        var job1 = Guid.NewGuid();
        var job2 = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        // Insert matches directly (no projection) — simulates pre-migration data.
        db.OpportunityMatches.AddRange(
            Match(job1, a, 50, Base),                 // older
            Match(job1, a, 88, Base.AddMinutes(5)),   // newer -> wins
            Match(job1, b, 33, Base),
            Match(job2, a, 70, Base));
        await db.SaveChangesAsync();

        var result = await Proj(db).BackfillAsync(default);

        Assert.Equal(3, result.ProcessedPairs); // (job1,a) (job1,b) (job2,a)
        Assert.Equal(3, result.Created);
        Assert.Equal(88, (await db.LatestOpportunityMatches.SingleAsync(r => r.JobPostingId == job1 && r.CandidateProfileId == a)).OverallScore);
    }

    [Fact]
    public void Domain_UpdateFrom_RejectsAnotherProfilesMatch()
    {
        var job = Guid.NewGuid();
        var projection = LatestOpportunityMatch.From(Match(job, Guid.NewGuid(), 90, Base));
        var otherProfile = Match(job, Guid.NewGuid(), 90, Base.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => projection.UpdateFrom(otherProfile));
    }
}
