using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddApiPeruConsultation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiPeruConsultations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ResponseData = table.Column<string>(type: "text", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PersonName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Condition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsultedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiPeruConsultations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiPeruConsultations_CompanyName",
                table: "ApiPeruConsultations",
                column: "CompanyName");

            migrationBuilder.CreateIndex(
                name: "IX_ApiPeruConsultations_ConsultedAt",
                table: "ApiPeruConsultations",
                column: "ConsultedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiPeruConsultations_DocumentNumber_DocumentType",
                table: "ApiPeruConsultations",
                columns: new[] { "DocumentNumber", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiPeruConsultations_PersonName",
                table: "ApiPeruConsultations",
                column: "PersonName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiPeruConsultations");
        }
    }
}
