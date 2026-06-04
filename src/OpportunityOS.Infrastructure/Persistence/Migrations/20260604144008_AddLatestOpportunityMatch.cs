using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLatestOpportunityMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "latest_opportunity_matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallScore = table.Column<int>(type: "integer", nullable: false),
                    Recommendation = table.Column<int>(type: "integer", nullable: false),
                    EngineVersion = table.Column<string>(type: "text", nullable: false),
                    MatchCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_latest_opportunity_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_latest_opportunity_matches_candidate_profiles_CandidateProf~",
                        column: x => x.CandidateProfileId,
                        principalTable: "candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_latest_opportunity_matches_job_postings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_latest_opportunity_matches_opportunity_matches_OpportunityM~",
                        column: x => x.OpportunityMatchId,
                        principalTable: "opportunity_matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_latest_opportunity_matches_CandidateProfileId_OverallScore",
                table: "latest_opportunity_matches",
                columns: new[] { "CandidateProfileId", "OverallScore" });

            migrationBuilder.CreateIndex(
                name: "IX_latest_opportunity_matches_CandidateProfileId_UpdatedAtUtc",
                table: "latest_opportunity_matches",
                columns: new[] { "CandidateProfileId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_latest_opportunity_matches_JobPostingId_CandidateProfileId",
                table: "latest_opportunity_matches",
                columns: new[] { "JobPostingId", "CandidateProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_latest_opportunity_matches_OpportunityMatchId",
                table: "latest_opportunity_matches",
                column: "OpportunityMatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "latest_opportunity_matches");
        }
    }
}
