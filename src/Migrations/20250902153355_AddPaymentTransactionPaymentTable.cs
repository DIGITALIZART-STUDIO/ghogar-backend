using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactionPaymentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactionPayments_Payments_PaymentId",
                table: "PaymentTransactionPayments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentTransactionPayments",
                table: "PaymentTransactionPayments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactionPayments_PaymentTransactionId",
                table: "PaymentTransactionPayments");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "PaymentTransactionPayments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentTransactionPayments",
                table: "PaymentTransactionPayments",
                columns: new[] { "PaymentTransactionId", "PaymentId" });

            migrationBuilder.CreateTable(
                name: "PaymentTransactionPaymentLegacy",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactionPaymentLegacy", x => new { x.PaymentId, x.PaymentTransactionId });
                    table.ForeignKey(
                        name: "FK_PaymentTransactionPaymentLegacy_PaymentTransactions_Payment~",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentTransactionPaymentLegacy_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactionPayments_PaymentId",
                table: "PaymentTransactionPayments",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactionPaymentLegacy_PaymentTransactionId",
                table: "PaymentTransactionPaymentLegacy",
                column: "PaymentTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactionPayments_Payments_PaymentId",
                table: "PaymentTransactionPayments",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactionPayments_Payments_PaymentId",
                table: "PaymentTransactionPayments");

            migrationBuilder.DropTable(
                name: "PaymentTransactionPaymentLegacy");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentTransactionPayments",
                table: "PaymentTransactionPayments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactionPayments_PaymentId",
                table: "PaymentTransactionPayments");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "PaymentTransactionPayments");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentTransactionPayments",
                table: "PaymentTransactionPayments",
                columns: new[] { "PaymentId", "PaymentTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactionPayments_PaymentTransactionId",
                table: "PaymentTransactionPayments",
                column: "PaymentTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactionPayments_Payments_PaymentId",
                table: "PaymentTransactionPayments",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
