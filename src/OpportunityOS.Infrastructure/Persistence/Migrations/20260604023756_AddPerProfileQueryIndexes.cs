using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerProfileQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_opportunity_matches_JobPostingId_CandidateProfileId",
                table: "opportunity_matches");

            migrationBuilder.CreateIndex(
                name: "IX_user_feedbacks_CandidateProfileId_JobPostingId_Type",
                table: "user_feedbacks",
                columns: new[] { "CandidateProfileId", "JobPostingId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_opportunity_matches_JobPostingId_CandidateProfileId_Created~",
                table: "opportunity_matches",
                columns: new[] { "JobPostingId", "CandidateProfileId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_generated_messages_CandidateProfileId_JobPostingId",
                table: "generated_messages",
                columns: new[] { "CandidateProfileId", "JobPostingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_feedbacks_CandidateProfileId_JobPostingId_Type",
                table: "user_feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_opportunity_matches_JobPostingId_CandidateProfileId_Created~",
                table: "opportunity_matches");

            migrationBuilder.DropIndex(
                name: "IX_generated_messages_CandidateProfileId_JobPostingId",
                table: "generated_messages");

            migrationBuilder.CreateIndex(
                name: "IX_opportunity_matches_JobPostingId_CandidateProfileId",
                table: "opportunity_matches",
                columns: new[] { "JobPostingId", "CandidateProfileId" });
        }
    }
}
