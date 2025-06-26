using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadRecyclingAndExpirationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Leads",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "CaptureSource",
                table: "Leads",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "CompletionReason",
                table: "Leads",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "EntryDate",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRecycledAt",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "LastRecycledById",
                table: "Leads",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Leads",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "RecycleCount",
                table: "Leads",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateTable(
                name: "LeadActivity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ActivityDate = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
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
                    table.PrimaryKey("PK_LeadActivity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadActivity_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_LeadActivity_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LastRecycledById",
                table: "Leads",
                column: "LastRecycledById"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ProjectId",
                table: "Leads",
                column: "ProjectId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivity_LeadId",
                table: "LeadActivity",
                column: "LeadId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LeadActivity_UserId",
                table: "LeadActivity",
                column: "UserId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_AspNetUsers_LastRecycledById",
                table: "Leads",
                column: "LastRecycledById",
                principalTable: "AspNetUsers",
                principalColumn: "Id"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Projects_ProjectId",
                table: "Leads",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_AspNetUsers_LastRecycledById",
                table: "Leads"
            );

            migrationBuilder.DropForeignKey(name: "FK_Leads_Projects_ProjectId", table: "Leads");

            migrationBuilder.DropTable(name: "LeadActivity");

            migrationBuilder.DropIndex(name: "IX_Leads_LastRecycledById", table: "Leads");

            migrationBuilder.DropIndex(name: "IX_Leads_ProjectId", table: "Leads");

            migrationBuilder.DropColumn(name: "CancellationReason", table: "Leads");

            migrationBuilder.DropColumn(name: "CaptureSource", table: "Leads");

            migrationBuilder.DropColumn(name: "CompletionReason", table: "Leads");

            migrationBuilder.DropColumn(name: "EntryDate", table: "Leads");

            migrationBuilder.DropColumn(name: "ExpirationDate", table: "Leads");

            migrationBuilder.DropColumn(name: "LastRecycledAt", table: "Leads");

            migrationBuilder.DropColumn(name: "LastRecycledById", table: "Leads");

            migrationBuilder.DropColumn(name: "ProjectId", table: "Leads");

            migrationBuilder.DropColumn(name: "RecycleCount", table: "Leads");
        }
    }
}
