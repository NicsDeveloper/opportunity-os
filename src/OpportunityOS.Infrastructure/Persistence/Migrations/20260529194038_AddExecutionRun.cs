using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "execution_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ItemsProcessed = table.Column<int>(type: "integer", nullable: false),
                    ItemsSucceeded = table.Column<int>(type: "integer", nullable: false),
                    ItemsFailed = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_runs_RunType",
                table: "execution_runs",
                column: "RunType");

            migrationBuilder.CreateIndex(
                name: "IX_execution_runs_StartedAtUtc",
                table: "execution_runs",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_runs");
        }
    }
}
