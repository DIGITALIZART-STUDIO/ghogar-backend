using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionHogar.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpCodeAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "OtpCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "OtpCodes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserId",
                table: "OtpCodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_ApprovedByUserId",
                table: "OtpCodes",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_RequestedByUserId",
                table: "OtpCodes",
                column: "RequestedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OtpCodes_AspNetUsers_ApprovedByUserId",
                table: "OtpCodes",
                column: "ApprovedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OtpCodes_AspNetUsers_RequestedByUserId",
                table: "OtpCodes",
                column: "RequestedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OtpCodes_AspNetUsers_ApprovedByUserId",
                table: "OtpCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_OtpCodes_AspNetUsers_RequestedByUserId",
                table: "OtpCodes");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_ApprovedByUserId",
                table: "OtpCodes");

            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_RequestedByUserId",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "OtpCodes");
        }
    }
}
