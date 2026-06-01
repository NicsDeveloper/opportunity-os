using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceQualityToJobPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalJobUrl",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RealCompanyName",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresManualValidation",
                table: "job_postings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SourceConfidenceScore",
                table: "job_postings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "job_postings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalJobUrl",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "RealCompanyName",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "RequiresManualValidation",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "SourceConfidenceScore",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "job_postings");
        }
    }
}
