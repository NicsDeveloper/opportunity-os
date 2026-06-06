using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float[]>(
                name: "Embedding",
                table: "job_postings",
                type: "real[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float[]>(
                name: "Embedding",
                table: "candidate_profiles",
                type: "real[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "candidate_profiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "candidate_profiles");
        }
    }
}
