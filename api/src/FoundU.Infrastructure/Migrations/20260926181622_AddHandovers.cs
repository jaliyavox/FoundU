using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHandovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PausedUntil",
                table: "LostReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectedAt",
                table: "LostReportFoundClaims",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CollectedByStaffId",
                table: "LostReportFoundClaims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionCheck",
                table: "LostReportFoundClaims",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FoundReportId",
                table: "LostReportFoundClaims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HandedInAt",
                table: "LostReportFoundClaims",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoverCode",
                table: "LostReportFoundClaims",
                type: "character(6)",
                fixedLength: true,
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HandoverExpiresAt",
                table: "LostReportFoundClaims",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HandoverStartedAt",
                table: "LostReportFoundClaims",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceivedByStaffId",
                table: "LostReportFoundClaims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "LostReportFoundClaims",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_LostReportFoundClaims_CollectedByStaffId",
                table: "LostReportFoundClaims",
                column: "CollectedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReportFoundClaims_FoundReportId",
                table: "LostReportFoundClaims",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReportFoundClaims_HandoverCode_Unique",
                table: "LostReportFoundClaims",
                column: "HandoverCode",
                unique: true,
                filter: "\"HandoverCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LostReportFoundClaims_ReceivedByStaffId",
                table: "LostReportFoundClaims",
                column: "ReceivedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_LostReportFoundClaims_AppUsers_CollectedByStaffId",
                table: "LostReportFoundClaims",
                column: "CollectedByStaffId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LostReportFoundClaims_AppUsers_ReceivedByStaffId",
                table: "LostReportFoundClaims",
                column: "ReceivedByStaffId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LostReportFoundClaims_FoundReports_FoundReportId",
                table: "LostReportFoundClaims",
                column: "FoundReportId",
                principalTable: "FoundReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LostReportFoundClaims_AppUsers_CollectedByStaffId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_LostReportFoundClaims_AppUsers_ReceivedByStaffId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_LostReportFoundClaims_FoundReports_FoundReportId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropIndex(
                name: "IX_LostReportFoundClaims_CollectedByStaffId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropIndex(
                name: "IX_LostReportFoundClaims_FoundReportId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropIndex(
                name: "IX_LostReportFoundClaims_HandoverCode_Unique",
                table: "LostReportFoundClaims");

            migrationBuilder.DropIndex(
                name: "IX_LostReportFoundClaims_ReceivedByStaffId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "PausedUntil",
                table: "LostReports");

            migrationBuilder.DropColumn(
                name: "CollectedAt",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "CollectedByStaffId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "CollectionCheck",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "FoundReportId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "HandedInAt",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "HandoverCode",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "HandoverExpiresAt",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "HandoverStartedAt",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "ReceivedByStaffId",
                table: "LostReportFoundClaims");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LostReportFoundClaims");
        }
    }
}
