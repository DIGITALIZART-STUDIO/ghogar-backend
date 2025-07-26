using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class PaymentsAssigned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTransactionId",
                table: "Payments",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentDate = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ModifiedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentTransactionId",
                table: "Payments",
                column: "PaymentTransactionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ReservationId",
                table: "PaymentTransactions",
                column: "ReservationId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentTransactions_PaymentTransactionId",
                table: "Payments",
                column: "PaymentTransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentTransactions_PaymentTransactionId",
                table: "Payments"
            );

            migrationBuilder.DropTable(name: "PaymentTransactions");

            migrationBuilder.DropIndex(name: "IX_Payments_PaymentTransactionId", table: "Payments");

            migrationBuilder.DropColumn(name: "PaymentTransactionId", table: "Payments");
        }
    }
}
