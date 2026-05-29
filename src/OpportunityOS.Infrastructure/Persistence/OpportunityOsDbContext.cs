using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OpportunityOS.Domain.Entities;

namespace OpportunityOS.Infrastructure.Persistence;

public sealed class OpportunityOsDbContext : DbContext
{
    public OpportunityOsDbContext(DbContextOptions<OpportunityOsDbContext> options) : base(options) { }

    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<OpportunityMatch> OpportunityMatches => Set<OpportunityMatch>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Value converters/comparers for List<string> stored as jsonb.
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, c) => (a ?? new()).SequenceEqual(c ?? new()),
            v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v == null ? new() : v.ToList());

        b.Entity<CandidateProfile>(e =>
        {
            e.ToTable("candidate_profiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).IsRequired();
            foreach (var prop in new[]
                     {
                         nameof(CandidateProfile.CoreSkills), nameof(CandidateProfile.SecondarySkills),
                         nameof(CandidateProfile.Domains), nameof(CandidateProfile.PreferredRoles),
                         nameof(CandidateProfile.PreferredContractTypes), nameof(CandidateProfile.PreferredLocations)
                     })
            {
                e.Property<List<string>>(prop)
                    .HasConversion(stringListConverter)
                    .Metadata.SetValueComparer(stringListComparer);
                e.Property<List<string>>(prop).HasColumnType("jsonb");
            }

            // Experiences serialized as a single jsonb column.
            var expConverter = new ValueConverter<List<CandidateExperience>, string>(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<CandidateExperience>>(v, jsonOptions) ?? new());
            var expComparer = new ValueComparer<List<CandidateExperience>>(
                (a, c) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(c, jsonOptions),
                v => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                v => JsonSerializer.Deserialize<List<CandidateExperience>>(JsonSerializer.Serialize(v, jsonOptions), jsonOptions) ?? new());
            e.Property(x => x.Experiences)
                .HasConversion(expConverter)
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(expComparer);
        });

        b.Entity<Company>(e =>
        {
            e.ToTable("companies");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Priority).HasConversion<int>();
            e.Property(x => x.Source).HasConversion<int>();
            e.Property(x => x.Tags).HasConversion(stringListConverter).HasColumnType("jsonb")
                .Metadata.SetValueComparer(stringListComparer);
            e.HasIndex(x => x.Priority);
            e.HasIndex(x => x.LastScannedAtUtc);
        });

        b.Entity<JobPosting>(e =>
        {
            e.ToTable("job_postings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.ExtractedSkills).HasConversion(stringListConverter).HasColumnType("jsonb")
                .Metadata.SetValueComparer(stringListComparer);
            e.Property(x => x.ExtractedDomains).HasConversion(stringListConverter).HasColumnType("jsonb")
                .Metadata.SetValueComparer(stringListComparer);
            e.HasIndex(x => x.CompanyId);
            e.HasIndex(x => new { x.SourceProvider, x.ExternalId }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        b.Entity<OpportunityMatch>(e =>
        {
            e.ToTable("opportunity_matches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Recommendation).HasConversion<int>();
            e.Property(x => x.Strengths).HasConversion(stringListConverter).HasColumnType("jsonb")
                .Metadata.SetValueComparer(stringListComparer);
            e.Property(x => x.Risks).HasConversion(stringListConverter).HasColumnType("jsonb")
                .Metadata.SetValueComparer(stringListComparer);
            e.Property(x => x.MissingRequirements).HasConversion(stringListConverter).HasColumnType("jsonb")
                .Metadata.SetValueComparer(stringListComparer);
            e.HasIndex(x => x.OverallScore);
            e.HasIndex(x => x.JobPostingId);
        });
    }
}
