using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactionPaymentsRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentTransactions_PaymentTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentTransactionId",
                table: "Payments");

            migrationBuilder.CreateTable(
                name: "PaymentTransactionPayments",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactionPayments", x => new { x.PaymentId, x.PaymentTransactionId });
                    table.ForeignKey(
                        name: "FK_PaymentTransactionPayments_PaymentTransactions_PaymentTrans~",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentTransactionPayments_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactionPayments_PaymentTransactionId",
                table: "PaymentTransactionPayments",
                column: "PaymentTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransactionPayments");

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTransactionId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentTransactionId",
                table: "Payments",
                column: "PaymentTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentTransactions_PaymentTransactionId",
                table: "Payments",
                column: "PaymentTransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id");
        }
    }
}
