using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddSupervisorSalesAdvisorTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupervisorSalesAdvisors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesAdvisorId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SupervisorSalesAdvisors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupervisorSalesAdvisors_AspNetUsers_SalesAdvisorId",
                        column: x => x.SalesAdvisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_SupervisorSalesAdvisors_AspNetUsers_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorSalesAdvisors_SalesAdvisorId",
                table: "SupervisorSalesAdvisors",
                column: "SalesAdvisorId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorSalesAdvisors_SupervisorId",
                table: "SupervisorSalesAdvisors",
                column: "SupervisorId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorSalesAdvisors_SupervisorId_SalesAdvisorId",
                table: "SupervisorSalesAdvisors",
                columns: new[] { "SupervisorId", "SalesAdvisorId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SupervisorSalesAdvisors");
        }
    }
}
