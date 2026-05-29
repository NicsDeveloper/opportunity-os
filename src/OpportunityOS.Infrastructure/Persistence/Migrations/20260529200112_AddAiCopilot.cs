using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCopilot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generated_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedInMessage = table.Column<string>(type: "text", nullable: false),
                    CoverLetter = table.Column<string>(type: "text", nullable: false),
                    EmailSubject = table.Column<string>(type: "text", nullable: false),
                    EmailBody = table.Column<string>(type: "text", nullable: false),
                    CvTailoringNotes = table.Column<string>(type: "text", nullable: false),
                    FollowUpMessage = table.Column<string>(type: "text", nullable: false),
                    HumanReviewNotes = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prompt_execution_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Service = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    UsedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RawResponse = table.Column<string>(type: "text", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_execution_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generated_messages_JobPostingId",
                table: "generated_messages",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_generated_messages_OpportunityMatchId",
                table: "generated_messages",
                column: "OpportunityMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_execution_logs_CreatedAtUtc",
                table: "prompt_execution_logs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_execution_logs_JobPostingId",
                table: "prompt_execution_logs",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_execution_logs_Service",
                table: "prompt_execution_logs",
                column: "Service");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_messages");

            migrationBuilder.DropTable(
                name: "prompt_execution_logs");
        }
    }
}
