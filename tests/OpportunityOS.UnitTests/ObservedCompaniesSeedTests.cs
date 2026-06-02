using Microsoft.EntityFrameworkCore;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;
using OpportunityOS.Infrastructure.Persistence;
using Xunit;

namespace OpportunityOS.UnitTests;

public sealed class ObservedCompaniesSeedTests
{
    private static OpportunityOsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<OpportunityOsDbContext>()
            .UseInMemoryDatabase("seed-" + Guid.NewGuid()).Options);

    [Theory]
    [InlineData("LanceSoft, Inc.", "LanceSoft Inc")]
    [InlineData("Pyramid Consulting, Inc", "Pyramid Consulting Inc")]
    [InlineData("Claranet Brasil", "Claranet")]
    [InlineData("Montreal Oficial", "montreal")]
    public void Normalize_TreatsVariantsAsSameName(string a, string b) =>
        Assert.Equal(ObservedCompaniesSeed.Normalize(a), ObservedCompaniesSeed.Normalize(b));

    [Fact]
    public async Task Seed_CreatesCompanies_WithTagsPriorityAndDiscoveryFlags()
    {
        using var db = NewDb();
        var s = await ObservedCompaniesSeed.RunAsync(db);

        Assert.True(s.Created > 50);
        Assert.Equal(0, s.Updated);
        Assert.True(s.Strategic >= 4);   // Fin-X, Blu, Nubank, BriteCore

        var nubank = await db.Companies.FirstAsync(c => c.Name == "Nubank");
        Assert.Equal(CompanyPriority.Strategic, nubank.Priority);
        Assert.Contains("fintech", nubank.Tags);
        Assert.Contains("observed-linkedin", nubank.Tags);
        Assert.Contains("needs-website-discovery", nubank.Tags);
        Assert.Contains("needs-ats-detection", nubank.Tags);

        var consultancy = await db.Companies.FirstAsync(c => c.Name == "Avenue Code");
        Assert.Contains("consulting", consultancy.Tags);
        var marketplace = await db.Companies.FirstAsync(c => c.Name == "Proxify");
        Assert.Contains("remote-marketplace", marketplace.Tags);
    }

    [Fact]
    public async Task Seed_IsIdempotent_NoDuplicatesOnSecondRun()
    {
        using var db = NewDb();
        var first = await ObservedCompaniesSeed.RunAsync(db);
        var countAfterFirst = await db.Companies.CountAsync();
        var second = await ObservedCompaniesSeed.RunAsync(db);
        var countAfterSecond = await db.Companies.CountAsync();

        Assert.Equal(countAfterFirst, countAfterSecond);
        Assert.Equal(0, second.Created);
        Assert.True(second.Updated > 50);
    }

    [Fact]
    public async Task Seed_DoesNotOverwriteGoodData_AndDoesNotDowngradePriority()
    {
        using var db = NewDb();
        // Pre-existing company with a website + already Strategic + a name variant.
        db.Companies.Add(new Company("LanceSoft Inc", "https://lancesoft.com", "https://boards.greenhouse.io/lancesoft",
            null, null, "BR", CompanyPriority.Strategic, CompanySource.Manual, new[] { "existing-tag" }));
        await db.SaveChangesAsync();

        await ObservedCompaniesSeed.RunAsync(db);

        // "LanceSoft Inc." (seed) dedups to the existing "LanceSoft Inc" -> still 1 row.
        var matches = await db.Companies.Where(c => c.Name.StartsWith("LanceSoft")).ToListAsync();
        var c = Assert.Single(matches);
        Assert.Equal("https://lancesoft.com", c.WebsiteUrl);            // not overwritten
        Assert.Equal("https://boards.greenhouse.io/lancesoft", c.CareersUrl); // not overwritten
        Assert.Equal(CompanyPriority.Strategic, c.Priority);            // not downgraded (seed says High)
        Assert.Contains("existing-tag", c.Tags);                        // kept
        Assert.Contains("consulting", c.Tags);                          // merged
        Assert.DoesNotContain("needs-website-discovery", c.Tags);       // has website -> not flagged
        Assert.DoesNotContain("needs-ats-detection", c.Tags);          // has careers -> not flagged
    }

    [Fact]
    public async Task Seed_RecordsExecutionRun()
    {
        using var db = NewDb();
        await ObservedCompaniesSeed.RunAsync(db);
        Assert.True(await db.ExecutionRuns.AnyAsync(r => r.RunType == "ObservedCompaniesSeed"));
    }
}
