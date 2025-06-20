using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxDiscountPercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaxDiscountPercentage",
                table: "Projects",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxDiscountPercentage",
                table: "Projects");
        }
    }
}
