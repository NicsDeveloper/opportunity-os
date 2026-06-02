using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpportunityOS.Domain.Entities;
using OpportunityOS.Domain.Enums;

namespace OpportunityOS.Infrastructure.Persistence;

/// <summary>
/// Internal seed (NOT a user feature) that adds companies the user observed manually on
/// LinkedIn into the EXISTING radar (Company), so the current discovery flow
/// (Company → Website Discovery → Career Page → ATS Detection → Job Discovery) can reach them.
/// Idempotent: never duplicates, never overwrites a good WebsiteUrl/CareersUrl, only raises
/// priority and merges tags. No LinkedIn access, no scraping, no applications.
/// </summary>
public static partial class ObservedCompaniesSeed
{
    private enum Cat { Financial, Consulting, Marketplace, Product, Misc }

    private sealed record Seed(string Name, CompanyPriority Priority, Cat Category, string[]? Extra = null);

    // Classification + priority taken from the user's manual LinkedIn observations.
    private static readonly Seed[] Companies =
    {
        // Financial / Strategic
        new("Fin-X", CompanyPriority.Strategic, Cat.Financial),
        new("Blu", CompanyPriority.Strategic, Cat.Financial),
        new("Nubank", CompanyPriority.Strategic, Cat.Financial),
        new("BriteCore", CompanyPriority.Strategic, Cat.Financial, new[] { "insurance", "financial-systems" }),
        // Consulting / Staffing — High
        new("Grupo GBI", CompanyPriority.High, Cat.Consulting, new[] { "dotnet-source" }),
        new("Montreal Oficial", CompanyPriority.High, Cat.Consulting, new[] { "dotnet-source" }),
        new("Claranet Brasil", CompanyPriority.High, Cat.Consulting, new[] { "cloud" }),
        new("Itix", CompanyPriority.High, Cat.Consulting),
        new("Meta", CompanyPriority.High, Cat.Consulting, new[] { "fullstack" }),
        new("Insight Global", CompanyPriority.High, Cat.Consulting),
        new("VDart Digital", CompanyPriority.High, Cat.Consulting, new[] { "dotnet-source" }),
        new("Pride Global", CompanyPriority.High, Cat.Consulting),
        new("Modus Create", CompanyPriority.High, Cat.Consulting),
        new("Avenue Code", CompanyPriority.High, Cat.Consulting, new[] { "fullstack" }),
        new("Nearform", CompanyPriority.High, Cat.Consulting),
        new("LanceSoft Inc.", CompanyPriority.High, Cat.Consulting, new[] { "dotnet-source" }),
        new("Amaris Consulting", CompanyPriority.High, Cat.Consulting),
        // Consulting / Staffing — Medium
        new("A3Data", CompanyPriority.Medium, Cat.Consulting, new[] { "data" }),
        new("Supranet", CompanyPriority.Medium, Cat.Consulting, new[] { "fullstack" }),
        new("M-Tech", CompanyPriority.Medium, Cat.Consulting),
        new("Noorden Group", CompanyPriority.Medium, Cat.Consulting, new[] { "ai" }),
        new("Vertex Agility", CompanyPriority.Medium, Cat.Consulting),
        new("GeorgiaTEK Systems Inc.", CompanyPriority.Medium, Cat.Consulting),
        new("Lazer Technologies", CompanyPriority.Medium, Cat.Consulting, new[] { "fullstack" }),
        new("Curotec", CompanyPriority.Medium, Cat.Consulting, new[] { "fullstack" }),
        new("HeartCentrix Solutions", CompanyPriority.Medium, Cat.Consulting, new[] { "fullstack" }),
        new("Pyramid Consulting Inc.", CompanyPriority.Medium, Cat.Consulting),
        new("Search Wizards", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Signify Technology", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Understanding Solutions", CompanyPriority.Medium, Cat.Consulting),
        new("Velozient", CompanyPriority.Medium, Cat.Consulting),
        new("OTIMIZE Tecnologia em Movimento", CompanyPriority.Medium, Cat.Consulting),
        new("Hays", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Brunel", CompanyPriority.Medium, Cat.Consulting),
        new("Fullinfo", CompanyPriority.Medium, Cat.Consulting),
        new("HighlightTA", CompanyPriority.Medium, Cat.Consulting, new[] { "recruiting" }),
        new("Hyqoo", CompanyPriority.Medium, Cat.Consulting, new[] { "staffing" }),
        // Marketplace
        new("Kake", CompanyPriority.High, Cat.Marketplace, new[] { "dotnet-source", "latam" }),
        new("Proxify", CompanyPriority.High, Cat.Marketplace),
        new("Alignerr", CompanyPriority.Medium, Cat.Marketplace, new[] { "ai" }),
        new("G2i Inc.", CompanyPriority.Medium, Cat.Marketplace, new[] { "ai" }),
        new("micro1", CompanyPriority.Medium, Cat.Marketplace, new[] { "ai" }),
        new("SME Careers", CompanyPriority.Medium, Cat.Marketplace),
        new("Karat", CompanyPriority.Low, Cat.Marketplace, new[] { "interview" }),
        new("Prolific", CompanyPriority.Low, Cat.Marketplace, new[] { "ai" }),
        // Direct employer / product — High
        new("Wave by Bemobi", CompanyPriority.High, Cat.Product),
        new("Conexa", CompanyPriority.High, Cat.Product, new[] { "healthtech" }),
        // Direct employer / product — Medium
        new("InPeace", CompanyPriority.Medium, Cat.Product),
        new("Azify", CompanyPriority.Medium, Cat.Product),
        new("Q4", CompanyPriority.Medium, Cat.Product, new[] { "global" }),
        new("Hyperproof", CompanyPriority.Medium, Cat.Product, new[] { "saas" }),
        new("Causa Certa", CompanyPriority.Medium, Cat.Product),
        new("Newsela", CompanyPriority.Medium, Cat.Product, new[] { "edtech" }),
        new("M3 USA", CompanyPriority.Medium, Cat.Product, new[] { "healthtech" }),
        new("Loadsmart", CompanyPriority.Medium, Cat.Product, new[] { "logistics" }),
        new("OpenAssets", CompanyPriority.Medium, Cat.Product, new[] { "saas" }),
        new("Jusfy", CompanyPriority.Medium, Cat.Product, new[] { "legaltech" }),
        new("AGGRANDIZE", CompanyPriority.Medium, Cat.Product),
        // Low — off-focus / talent pools / noisy (kept on radar, low priority + reason tag)
        new("Môre", CompanyPriority.Low, Cat.Product, new[] { "talent-pool" }),
        new("Seox Inteligência digital para Publishers", CompanyPriority.Low, Cat.Product, new[] { "talent-pool" }),
        new("Servant", CompanyPriority.Low, Cat.Product, new[] { "off-stack" }),
        new("AlphaSights", CompanyPriority.Low, Cat.Consulting, new[] { "off-stack" }),
        new("Alstra Technologies", CompanyPriority.Low, Cat.Product, new[] { "power-platform", "off-stack" }),
        new("Riveron", CompanyPriority.Low, Cat.Product, new[] { "mulesoft", "off-stack" }),
        new("ArcTouch", CompanyPriority.Low, Cat.Consulting, new[] { "mobile", "talent-pool" }),
        new("Techifide Ltd", CompanyPriority.Low, Cat.Product, new[] { "off-stack" }),
        new("Moralis", CompanyPriority.Low, Cat.Product, new[] { "web3", "off-stack" }),
        new("YO HR Consultancy", CompanyPriority.Low, Cat.Consulting, new[] { "recruiting", "noisy-source" }),
        new("Delivery Associates", CompanyPriority.Low, Cat.Product),
        new("Trentini Assessoria Previdenciária", CompanyPriority.Low, Cat.Misc, new[] { "off-stack" }),
    };

    private static readonly string[] GlobalTags = { "observed-linkedin", "manual-radar-seed" };

    private static string[] BaseTags(Cat c) => c switch
    {
        Cat.Financial => new[] { "financial", "fintech", "banking", "payments", "dotnet-priority", "remote-source" },
        Cat.Consulting => new[] { "consulting", "staffing", "outsourcing", "remote-source", "dotnet-source" },
        Cat.Marketplace => new[] { "remote-marketplace", "talent-network", "contractor", "remote-source", "noisy-source" },
        Cat.Product => new[] { "product-company", "direct-employer", "remote-source", "software-engineering" },
        _ => new[] { "remote-source" },
    };

    private static string? Industry(Cat c) => c switch
    {
        Cat.Financial => "Fintech / Financial",
        Cat.Consulting => "IT Consulting / Staffing",
        Cat.Marketplace => "Remote Talent Marketplace",
        Cat.Product => "Software / Product",
        _ => null,
    };

    public sealed record SeedSummary(
        int Received, int Created, int Updated, int Skipped,
        int Strategic, int High, int Medium, int Low,
        int NeedWebsite, int NeedAts, List<string> Warnings);

    public static async Task<SeedSummary> RunAsync(OpportunityOsDbContext db, CancellationToken ct = default)
    {
        var run = ExecutionRun.Start("ObservedCompaniesSeed");
        await db.ExecutionRuns.AddAsync(run, ct);

        var existing = await db.Companies.ToListAsync(ct);
        var byNorm = existing
            .GroupBy(c => Normalize(c.Name))
            .ToDictionary(g => g.Key, g => g.First());

        int created = 0, updated = 0, skipped = 0, needWebsite = 0, needAts = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var byPriority = new Dictionary<CompanyPriority, int>();

        foreach (var seed in Companies)
        {
            var norm = Normalize(seed.Name);
            if (!seen.Add(norm)) { skipped++; continue; } // intra-list dup

            var tags = GlobalTags
                .Concat(BaseTags(seed.Category))
                .Concat(seed.Extra ?? Array.Empty<string>())
                .ToList();

            Company company;
            if (byNorm.TryGetValue(norm, out var found))
            {
                company = found;
                foreach (var t in tags) company.AddTag(t);
                company.RaisePriorityTo(seed.Priority); // never downgrade
                updated++;
                run.RecordSuccess();
            }
            else
            {
                company = new Company(seed.Name, websiteUrl: null, careersUrl: null, linkedInUrl: null,
                    industry: Industry(seed.Category), country: null, seed.Priority, CompanySource.SearchDiscovery, tags);
                await db.Companies.AddAsync(company, ct);
                byNorm[norm] = company;
                created++;
                run.RecordSuccess();
            }

            // Mark for the existing discovery flow only when data is missing (don't overwrite).
            if (string.IsNullOrWhiteSpace(company.WebsiteUrl)) { company.AddTag("needs-website-discovery"); needWebsite++; }
            if (string.IsNullOrWhiteSpace(company.CareersUrl)) { company.AddTag("needs-ats-detection"); needAts++; }

            byPriority[company.Priority] = byPriority.GetValueOrDefault(company.Priority) + 1;
        }

        run.Complete();
        await db.SaveChangesAsync(ct);

        return new SeedSummary(
            Companies.Length, created, updated, skipped,
            byPriority.GetValueOrDefault(CompanyPriority.Strategic),
            byPriority.GetValueOrDefault(CompanyPriority.High),
            byPriority.GetValueOrDefault(CompanyPriority.Medium),
            byPriority.GetValueOrDefault(CompanyPriority.Low),
            needWebsite, needAts, warnings);
    }

    /// <summary>Normalize for dedup: lowercase, strip accents/punctuation and common suffixes
    /// (Inc, Ltd, LTDA, S.A., Oficial, Brasil).</summary>
    public static string Normalize(string name)
    {
        var lowered = (name ?? string.Empty).Trim().ToLowerInvariant();
        var noAccent = new string(lowered.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var cleaned = NonAlnumRegex().Replace(noAccent, " ");
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("inc" or "ltd" or "ltda" or "sa" or "oficial" or "brasil"))
            .ToArray();
        return string.Join(" ", words);
    }

    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonAlnumRegex();
}
