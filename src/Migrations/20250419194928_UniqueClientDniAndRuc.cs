using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class UniqueClientDniAndRuc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Client_Dni",
                table: "Clients",
                column: "Dni",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Client_Ruc",
                table: "Clients",
                column: "Ruc",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Client_Dni",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Client_Ruc",
                table: "Clients");
        }
    }
}
