using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecipientId",
                table: "LostReportMessages",
                type: "uuid",
                nullable: true);

            // Every existing message was a finder writing to the author.
            migrationBuilder.Sql("""
                UPDATE "LostReportMessages" m
                SET "RecipientId" = r."StudentId"
                FROM "LostReports" r
                WHERE r."Id" = m."LostReportId" AND m."RecipientId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LostReportMessages_RecipientId",
                table: "LostReportMessages",
                column: "RecipientId");

            migrationBuilder.AddForeignKey(
                name: "FK_LostReportMessages_AppUsers_RecipientId",
                table: "LostReportMessages",
                column: "RecipientId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LostReportMessages_AppUsers_RecipientId",
                table: "LostReportMessages");

            migrationBuilder.DropIndex(
                name: "IX_LostReportMessages_RecipientId",
                table: "LostReportMessages");

            migrationBuilder.DropColumn(
                name: "RecipientId",
                table: "LostReportMessages");
        }
    }
}
