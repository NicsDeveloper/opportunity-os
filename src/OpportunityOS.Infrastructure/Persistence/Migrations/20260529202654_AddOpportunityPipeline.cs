using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "opportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecruiterLeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextFollowUpAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_opportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "recruiter_leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    RoleTitle = table.Column<string>(type: "text", nullable: true),
                    LinkedInUrl = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recruiter_leads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_JobPostingId",
                table: "opportunities",
                column: "JobPostingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_NextFollowUpAtUtc",
                table: "opportunities",
                column: "NextFollowUpAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_opportunities_Status",
                table: "opportunities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_recruiter_leads_CompanyId",
                table: "recruiter_leads",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "opportunities");

            migrationBuilder.DropTable(
                name: "recruiter_leads");
        }
    }
}
