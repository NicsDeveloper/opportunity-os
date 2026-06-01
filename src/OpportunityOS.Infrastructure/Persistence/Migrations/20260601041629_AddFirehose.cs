using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFirehose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "raw_job_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Snippet = table.Column<string>(type: "text", nullable: true),
                    DiscoveredUrl = table.Column<string>(type: "text", nullable: false),
                    SourceProvider = table.Column<string>(type: "text", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    RealCompanyName = table.Column<string>(type: "text", nullable: true),
                    OriginalJobUrl = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    WorkMode = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceConfidenceScore = table.Column<int>(type: "integer", nullable: false),
                    PreliminaryFitScore = table.Column<int>(type: "integer", nullable: true),
                    NormalizedFingerprint = table.Column<string>(type: "text", nullable: true),
                    RequiresManualValidation = table.Column<bool>(type: "boolean", nullable: false),
                    SearchCampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Query = table.Column<string>(type: "text", nullable: true),
                    PromotedJobPostingId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_job_candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "search_campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    BaseKeywords = table.Column<string>(type: "jsonb", nullable: false),
                    TargetSources = table.Column<string>(type: "jsonb", nullable: false),
                    ExcludedDomains = table.Column<string>(type: "jsonb", nullable: false),
                    DailyQueryBudget = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "search_query_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchCampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResultsCount = table.Column<int>(type: "integer", nullable: false),
                    NewCandidatesCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateCandidatesCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_query_executions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "search_query_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Template = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_query_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_raw_job_candidates_DiscoveredAtUtc",
                table: "raw_job_candidates",
                column: "DiscoveredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_raw_job_candidates_DiscoveredUrl",
                table: "raw_job_candidates",
                column: "DiscoveredUrl");

            migrationBuilder.CreateIndex(
                name: "IX_raw_job_candidates_NormalizedFingerprint",
                table: "raw_job_candidates",
                column: "NormalizedFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_raw_job_candidates_SearchCampaignId",
                table: "raw_job_candidates",
                column: "SearchCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_raw_job_candidates_Status",
                table: "raw_job_candidates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_search_campaigns_Status",
                table: "search_campaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_search_query_executions_SearchCampaignId",
                table: "search_query_executions",
                column: "SearchCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_search_query_executions_StartedAtUtc",
                table: "search_query_executions",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_search_query_templates_Category",
                table: "search_query_templates",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "raw_job_candidates");

            migrationBuilder.DropTable(
                name: "search_campaigns");

            migrationBuilder.DropTable(
                name: "search_query_executions");

            migrationBuilder.DropTable(
                name: "search_query_templates");
        }
    }
}
