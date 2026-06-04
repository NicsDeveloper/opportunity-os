using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileToActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_opportunities_JobPostingId",
                table: "opportunities");

            migrationBuilder.AddColumn<Guid>(
                name: "CandidateProfileId",
                table: "user_feedbacks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CandidateProfileId",
                table: "opportunities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CandidateProfileId",
                table: "generated_messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill existing rows to the default profile (the single-profile data we had so far),
            // BEFORE the unique (JobPostingId, CandidateProfileId) index is created.
            const string defaultProfile =
                "(SELECT \"Id\" FROM candidate_profiles ORDER BY \"IsDefault\" DESC, \"CreatedAtUtc\" DESC LIMIT 1)";
            migrationBuilder.Sql(
                $"UPDATE opportunities SET \"CandidateProfileId\" = {defaultProfile} " +
                "WHERE \"CandidateProfileId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql(
                $"UPDATE generated_messages SET \"CandidateProfileId\" = {defaultProfile} " +
                "WHERE \"CandidateProfileId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql(
                $"UPDATE user_feedbacks SET \"CandidateProfileId\" = {defaultProfile} " +
                "WHERE \"CandidateProfileId\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_user_feedbacks_CandidateProfileId",
                table: "user_feedbacks",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_CandidateProfileId",
                table: "opportunities",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_JobPostingId_CandidateProfileId",
                table: "opportunities",
                columns: new[] { "JobPostingId", "CandidateProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_generated_messages_CandidateProfileId",
                table: "generated_messages",
                column: "CandidateProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_feedbacks_CandidateProfileId",
                table: "user_feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_opportunities_CandidateProfileId",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "IX_opportunities_JobPostingId_CandidateProfileId",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "IX_generated_messages_CandidateProfileId",
                table: "generated_messages");

            migrationBuilder.DropColumn(
                name: "CandidateProfileId",
                table: "user_feedbacks");

            migrationBuilder.DropColumn(
                name: "CandidateProfileId",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "CandidateProfileId",
                table: "generated_messages");

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_JobPostingId",
                table: "opportunities",
                column: "JobPostingId",
                unique: true);
        }
    }
}
