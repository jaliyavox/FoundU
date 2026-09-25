using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLostReportFlagReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagReason",
                table: "LostReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FlaggedAt",
                table: "LostReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlagged",
                table: "LostReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlagReason",
                table: "LostReports");

            migrationBuilder.DropColumn(
                name: "FlaggedAt",
                table: "LostReports");

            migrationBuilder.DropColumn(
                name: "IsFlagged",
                table: "LostReports");
        }
    }
}
