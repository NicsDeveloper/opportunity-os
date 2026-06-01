using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpportunityOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBacenInstitution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bacen_institutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Ispb = table.Column<string>(type: "text", nullable: true),
                    Cnpj = table.Column<string>(type: "text", nullable: true),
                    InstitutionType = table.Column<string>(type: "text", nullable: false),
                    AuthorizedByBacen = table.Column<bool>(type: "boolean", nullable: false),
                    SpiParticipationType = table.Column<string>(type: "text", nullable: true),
                    PixParticipationType = table.Column<string>(type: "text", nullable: true),
                    PixParticipationMode = table.Column<string>(type: "text", nullable: true),
                    PaymentInitiation = table.Column<bool>(type: "boolean", nullable: true),
                    CashoutServiceFacilitator = table.Column<bool>(type: "boolean", nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bacen_institutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bacen_institutions_AuthorizedByBacen",
                table: "bacen_institutions",
                column: "AuthorizedByBacen");

            migrationBuilder.CreateIndex(
                name: "IX_bacen_institutions_Cnpj",
                table: "bacen_institutions",
                column: "Cnpj",
                unique: true,
                filter: "\"Cnpj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bacen_institutions_Ispb",
                table: "bacen_institutions",
                column: "Ispb",
                unique: true,
                filter: "\"Ispb\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bacen_institutions");
        }
    }
}
