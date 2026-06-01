using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_feedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawJobCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_feedbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_feedbacks_JobPostingId",
                table: "user_feedbacks",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_user_feedbacks_RawJobCandidateId",
                table: "user_feedbacks",
                column: "RawJobCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_user_feedbacks_Type",
                table: "user_feedbacks",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_feedbacks");
        }
    }
}
