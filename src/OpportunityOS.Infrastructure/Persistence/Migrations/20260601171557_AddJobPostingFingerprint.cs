using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPostingFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedFingerprint",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_NormalizedFingerprint",
                table: "job_postings",
                column: "NormalizedFingerprint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_job_postings_NormalizedFingerprint",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "NormalizedFingerprint",
                table: "job_postings");
        }
    }
}
