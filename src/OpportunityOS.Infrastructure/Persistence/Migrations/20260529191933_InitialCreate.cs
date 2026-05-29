using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Seniority = table.Column<string>(type: "text", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "text", nullable: false),
                    CoreSkills = table.Column<string>(type: "jsonb", nullable: false),
                    SecondarySkills = table.Column<string>(type: "jsonb", nullable: false),
                    Domains = table.Column<string>(type: "jsonb", nullable: false),
                    PreferredRoles = table.Column<string>(type: "jsonb", nullable: false),
                    PreferredContractTypes = table.Column<string>(type: "jsonb", nullable: false),
                    PreferredLocations = table.Column<string>(type: "jsonb", nullable: false),
                    Experiences = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    CareersUrl = table.Column<string>(type: "text", nullable: true),
                    LinkedInUrl = table.Column<string>(type: "text", nullable: true),
                    Industry = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastScannedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_postings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    SourceProvider = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    WorkMode = table.Column<string>(type: "text", nullable: true),
                    Seniority = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    AbsoluteUrl = table.Column<string>(type: "text", nullable: false),
                    DescriptionHtml = table.Column<string>(type: "text", nullable: true),
                    DescriptionText = table.Column<string>(type: "text", nullable: false),
                    ExtractedSkills = table.Column<string>(type: "jsonb", nullable: false),
                    ExtractedDomains = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_postings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallScore = table.Column<int>(type: "integer", nullable: false),
                    TechnicalScore = table.Column<int>(type: "integer", nullable: false),
                    DomainScore = table.Column<int>(type: "integer", nullable: false),
                    SeniorityScore = table.Column<int>(type: "integer", nullable: false),
                    LocationScore = table.Column<int>(type: "integer", nullable: false),
                    LanguageScore = table.Column<int>(type: "integer", nullable: false),
                    Recommendation = table.Column<int>(type: "integer", nullable: false),
                    Strengths = table.Column<string>(type: "jsonb", nullable: false),
                    Risks = table.Column<string>(type: "jsonb", nullable: false),
                    MissingRequirements = table.Column<string>(type: "jsonb", nullable: false),
                    Rationale = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunity_matches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_companies_LastScannedAtUtc",
                table: "companies",
                column: "LastScannedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_companies_Priority",
                table: "companies",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_CompanyId",
                table: "job_postings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_SourceProvider_ExternalId",
                table: "job_postings",
                columns: new[] { "SourceProvider", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_Status",
                table: "job_postings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_opportunity_matches_JobPostingId",
                table: "opportunity_matches",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_opportunity_matches_OverallScore",
                table: "opportunity_matches",
                column: "OverallScore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_profiles");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "job_postings");

            migrationBuilder.DropTable(
                name: "opportunity_matches");
        }
    }
}
