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
    public DbSet<ExecutionRun> ExecutionRuns => Set<ExecutionRun>();
    public DbSet<GeneratedMessage> GeneratedMessages => Set<GeneratedMessage>();
    public DbSet<PromptExecutionLog> PromptExecutionLogs => Set<PromptExecutionLog>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<RecruiterLead> RecruiterLeads => Set<RecruiterLead>();
    public DbSet<BacenInstitution> BacenInstitutions => Set<BacenInstitution>();
    public DbSet<SearchCampaign> SearchCampaigns => Set<SearchCampaign>();
    public DbSet<SearchQueryTemplate> SearchQueryTemplates => Set<SearchQueryTemplate>();
    public DbSet<SearchQueryExecution> SearchQueryExecutions => Set<SearchQueryExecution>();
    public DbSet<RawJobCandidate> RawJobCandidates => Set<RawJobCandidate>();

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

        b.Entity<ExecutionRun>(e =>
        {
            e.ToTable("execution_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.RunType).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => x.RunType);
            e.HasIndex(x => x.StartedAtUtc);
        });

        b.Entity<GeneratedMessage>(e =>
        {
            e.ToTable("generated_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => x.JobPostingId);
            e.HasIndex(x => x.OpportunityMatchId);
        });

        b.Entity<PromptExecutionLog>(e =>
        {
            e.ToTable("prompt_execution_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Service).IsRequired();
            e.Property(x => x.PromptVersion).IsRequired();
            e.HasIndex(x => x.Service);
            e.HasIndex(x => x.JobPostingId);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        b.Entity<Opportunity>(e =>
        {
            e.ToTable("opportunities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.JobPostingId).IsUnique();
            e.HasIndex(x => x.NextFollowUpAtUtc);
        });

        b.Entity<RecruiterLead>(e =>
        {
            e.ToTable("recruiter_leads");
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).IsRequired();
            e.Property(x => x.Source).HasConversion<int>();
            e.HasIndex(x => x.CompanyId);
        });

        b.Entity<BacenInstitution>(e =>
        {
            e.ToTable("bacen_institutions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.InstitutionType).IsRequired();
            e.Property(x => x.Tags).HasConversion(stringListConverter).HasColumnType("jsonb")
                .Metadata.SetValueComparer(stringListComparer);
            // Preferred unique key on CNPJ when present; alternate on ISPB.
            e.HasIndex(x => x.Cnpj).IsUnique().HasFilter("\"Cnpj\" IS NOT NULL");
            e.HasIndex(x => x.Ispb).IsUnique().HasFilter("\"Ispb\" IS NOT NULL");
            e.HasIndex(x => x.AuthorizedByBacen);
        });

        b.Entity<SearchCampaign>(e =>
        {
            e.ToTable("search_campaigns");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Priority).HasConversion<int>();
            foreach (var prop in new[] { nameof(SearchCampaign.BaseKeywords), nameof(SearchCampaign.TargetSources), nameof(SearchCampaign.ExcludedDomains) })
            {
                e.Property<List<string>>(prop).HasConversion(stringListConverter).HasColumnType("jsonb")
                    .Metadata.SetValueComparer(stringListComparer);
            }
            e.HasIndex(x => x.Status);
        });

        b.Entity<SearchQueryTemplate>(e =>
        {
            e.ToTable("search_query_templates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Template).IsRequired();
            e.HasIndex(x => x.Category);
        });

        b.Entity<SearchQueryExecution>(e =>
        {
            e.ToTable("search_query_executions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => x.SearchCampaignId);
            e.HasIndex(x => x.StartedAtUtc);
        });

        b.Entity<RawJobCandidate>(e =>
        {
            e.ToTable("raw_job_candidates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired();
            e.Property(x => x.DiscoveredUrl).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.SourceType).HasConversion<int>();
            e.HasIndex(x => x.DiscoveredUrl);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DiscoveredAtUtc);
            e.HasIndex(x => x.SearchCampaignId);
            e.HasIndex(x => x.NormalizedFingerprint);
        });
    }
}
