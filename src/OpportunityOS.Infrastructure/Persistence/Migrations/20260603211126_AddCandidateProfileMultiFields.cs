using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProfileMultiFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "candidate_profiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExcludedStacks",
                table: "candidate_profiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "candidate_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinimumScoreToShow",
                table: "candidate_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "PreferredWorkModes",
                table: "candidate_profiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_profiles_IsDefault",
                table: "candidate_profiles",
                column: "IsDefault");

            // Backfill existing data: DisplayName mirrors FullName, and the most recent profile
            // becomes the default anchor (compat with the single-profile behaviour).
            migrationBuilder.Sql(
                "UPDATE candidate_profiles SET \"DisplayName\" = \"FullName\" WHERE \"DisplayName\" = '';");
            migrationBuilder.Sql(
                "UPDATE candidate_profiles SET \"IsDefault\" = true WHERE \"Id\" = " +
                "(SELECT \"Id\" FROM candidate_profiles ORDER BY \"CreatedAtUtc\" DESC LIMIT 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_candidate_profiles_IsDefault",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "ExcludedStacks",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "MinimumScoreToShow",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "PreferredWorkModes",
                table: "candidate_profiles");
        }
    }
}
