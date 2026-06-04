using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultingCompanyCandidate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consulting_company_candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    LinkedInCompanyUrl = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Signals = table.Column<string>(type: "jsonb", nullable: false),
                    ConsultingConfidenceScore = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PromotedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consulting_company_candidates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consulting_company_candidates_ConsultingConfidenceScore",
                table: "consulting_company_candidates",
                column: "ConsultingConfidenceScore");

            migrationBuilder.CreateIndex(
                name: "IX_consulting_company_candidates_Name",
                table: "consulting_company_candidates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_consulting_company_candidates_Status",
                table: "consulting_company_candidates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consulting_company_candidates");
        }
    }
}
