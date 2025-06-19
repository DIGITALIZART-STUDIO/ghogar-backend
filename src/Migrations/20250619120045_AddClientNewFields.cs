using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddClientNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CoOwner",
                table: "Clients",
                newName: "Country");

            migrationBuilder.AddColumn<string>(
                name: "CoOwners",
                table: "Clients",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeparateProperty",
                table: "Clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SeparatePropertyData",
                table: "Clients",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Client_PhoneNumber",
                table: "Clients",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Client_PhoneNumber",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "CoOwners",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SeparateProperty",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "SeparatePropertyData",
                table: "Clients");

            migrationBuilder.RenameColumn(
                name: "Country",
                table: "Clients",
                newName: "CoOwner");
        }
    }
}
