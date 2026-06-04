using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchEngineVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EngineVersion",
                table: "opportunity_matches",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_opportunity_matches_JobPostingId_CandidateProfileId",
                table: "opportunity_matches",
                columns: new[] { "JobPostingId", "CandidateProfileId" });

            // Existing matches predate the profile-driven engine; tag them so a v2 rescore replaces them.
            migrationBuilder.Sql(
                "UPDATE opportunity_matches SET \"EngineVersion\" = 'heuristic-v1' WHERE \"EngineVersion\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_opportunity_matches_JobPostingId_CandidateProfileId",
                table: "opportunity_matches");

            migrationBuilder.DropColumn(
                name: "EngineVersion",
                table: "opportunity_matches");
        }
    }
}
