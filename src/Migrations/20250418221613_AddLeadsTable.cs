using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Procedency = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_AspNetUsers_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id"
                    );
                    table.ForeignKey(
                        name: "FK_Leads_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Leads_AssignedToId",
                table: "Leads",
                column: "AssignedToId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ClientId",
                table: "Leads",
                column: "ClientId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Leads");
        }
    }
}
