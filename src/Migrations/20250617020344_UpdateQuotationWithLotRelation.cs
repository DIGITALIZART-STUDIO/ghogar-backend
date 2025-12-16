using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class UpdateQuotationWithLotRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Block", table: "Quotations");

            migrationBuilder.DropColumn(name: "LotNumber", table: "Quotations");

            migrationBuilder.DropColumn(name: "ProjectName", table: "Quotations");

            migrationBuilder.RenameColumn(
                name: "PricePerM2",
                table: "Quotations",
                newName: "PricePerM2AtQuotation"
            );

            migrationBuilder.RenameColumn(
                name: "Area",
                table: "Quotations",
                newName: "AreaAtQuotation"
            );

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "Quotations",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)"
            );

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Quotations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<Guid>(
                name: "LotId",
                table: "Quotations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_LotId",
                table: "Quotations",
                column: "LotId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Lots_LotId",
                table: "Quotations",
                column: "LotId",
                principalTable: "Lots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Quotations_Lots_LotId", table: "Quotations");

            migrationBuilder.DropIndex(name: "IX_Quotations_LotId", table: "Quotations");

            migrationBuilder.DropColumn(name: "Currency", table: "Quotations");

            migrationBuilder.DropColumn(name: "LotId", table: "Quotations");

            migrationBuilder.RenameColumn(
                name: "PricePerM2AtQuotation",
                table: "Quotations",
                newName: "PricePerM2"
            );

            migrationBuilder.RenameColumn(
                name: "AreaAtQuotation",
                table: "Quotations",
                newName: "Area"
            );

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)"
            );

            migrationBuilder.AddColumn<string>(
                name: "Block",
                table: "Quotations",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                table: "Quotations",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "Quotations",
                type: "text",
                nullable: false,
                defaultValue: ""
            );
        }
    }
}
