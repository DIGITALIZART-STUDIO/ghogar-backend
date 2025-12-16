using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class UpdateQuotationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_AspNetUsers_CreatedById",
                table: "Quotations"
            );

            migrationBuilder.DropColumn(name: "Tax", table: "Quotations");

            migrationBuilder.DropColumn(name: "TotalAmount", table: "Quotations");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Quotations",
                newName: "ProjectName"
            );

            migrationBuilder.RenameColumn(
                name: "QuotationDate",
                table: "Quotations",
                newName: "LotNumber"
            );

            migrationBuilder.RenameColumn(
                name: "ExpirationDate",
                table: "Quotations",
                newName: "ValidUntil"
            );

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Quotations",
                newName: "Code"
            );

            migrationBuilder.RenameColumn(
                name: "CreatedById",
                table: "Quotations",
                newName: "AdvisorId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_CreatedById",
                table: "Quotations",
                newName: "IX_Quotations_AdvisorId"
            );

            migrationBuilder.AlterColumn<decimal>(
                name: "Discount",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric"
            );

            migrationBuilder.AddColumn<decimal>(
                name: "AmountFinanced",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "Area",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<string>(
                name: "Block",
                table: "Quotations",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<decimal>(
                name: "DownPayment",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "FinalPrice",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<int>(
                name: "MonthsFinanced",
                table: "Quotations",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerM2",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "Quotations",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_AspNetUsers_AdvisorId",
                table: "Quotations",
                column: "AdvisorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_AspNetUsers_AdvisorId",
                table: "Quotations"
            );

            migrationBuilder.DropColumn(name: "AmountFinanced", table: "Quotations");

            migrationBuilder.DropColumn(name: "Area", table: "Quotations");

            migrationBuilder.DropColumn(name: "Block", table: "Quotations");

            migrationBuilder.DropColumn(name: "DownPayment", table: "Quotations");

            migrationBuilder.DropColumn(name: "ExchangeRate", table: "Quotations");

            migrationBuilder.DropColumn(name: "FinalPrice", table: "Quotations");

            migrationBuilder.DropColumn(name: "MonthsFinanced", table: "Quotations");

            migrationBuilder.DropColumn(name: "PricePerM2", table: "Quotations");

            migrationBuilder.DropColumn(name: "TotalPrice", table: "Quotations");

            migrationBuilder.RenameColumn(
                name: "ValidUntil",
                table: "Quotations",
                newName: "ExpirationDate"
            );

            migrationBuilder.RenameColumn(
                name: "ProjectName",
                table: "Quotations",
                newName: "Title"
            );

            migrationBuilder.RenameColumn(
                name: "LotNumber",
                table: "Quotations",
                newName: "QuotationDate"
            );

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Quotations",
                newName: "Description"
            );

            migrationBuilder.RenameColumn(
                name: "AdvisorId",
                table: "Quotations",
                newName: "CreatedById"
            );

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_AdvisorId",
                table: "Quotations",
                newName: "IX_Quotations_CreatedById"
            );

            migrationBuilder.AlterColumn<decimal>(
                name: "Discount",
                table: "Quotations",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)"
            );

            migrationBuilder.AddColumn<decimal>(
                name: "Tax",
                table: "Quotations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Quotations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_AspNetUsers_CreatedById",
                table: "Quotations",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
