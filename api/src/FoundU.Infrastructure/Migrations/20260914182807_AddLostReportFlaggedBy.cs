using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLostReportFlaggedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FlaggedByUserId",
                table: "LostReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_FlaggedByUserId",
                table: "LostReports",
                column: "FlaggedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_IsFlagged_FlaggedAt",
                table: "LostReports",
                columns: new[] { "IsFlagged", "FlaggedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_LostReports_AppUsers_FlaggedByUserId",
                table: "LostReports",
                column: "FlaggedByUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LostReports_AppUsers_FlaggedByUserId",
                table: "LostReports");

            migrationBuilder.DropIndex(
                name: "IX_LostReports_FlaggedByUserId",
                table: "LostReports");

            migrationBuilder.DropIndex(
                name: "IX_LostReports_IsFlagged_FlaggedAt",
                table: "LostReports");

            migrationBuilder.DropColumn(
                name: "FlaggedByUserId",
                table: "LostReports");
        }
    }
}
