using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinderPostedFoundReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "StorageLocationId",
                table: "FoundReports",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "StaffId",
                table: "FoundReports",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FinderId",
                table: "FoundReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandInCode",
                table: "FoundReports",
                type: "character(6)",
                fixedLength: true,
                maxLength: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_FinderId",
                table: "FoundReports",
                column: "FinderId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_HandInCode",
                table: "FoundReports",
                column: "HandInCode",
                unique: true,
                filter: "\"HandInCode\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FoundReports_AppUsers_FinderId",
                table: "FoundReports",
                column: "FinderId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoundReports_AppUsers_FinderId",
                table: "FoundReports");

            migrationBuilder.DropIndex(
                name: "IX_FoundReports_FinderId",
                table: "FoundReports");

            migrationBuilder.DropIndex(
                name: "IX_FoundReports_HandInCode",
                table: "FoundReports");

            migrationBuilder.DropColumn(
                name: "FinderId",
                table: "FoundReports");

            migrationBuilder.DropColumn(
                name: "HandInCode",
                table: "FoundReports");

            migrationBuilder.AlterColumn<Guid>(
                name: "StorageLocationId",
                table: "FoundReports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StaffId",
                table: "FoundReports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
