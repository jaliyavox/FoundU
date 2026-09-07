using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLostReportFoundClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LostReportFoundClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LostReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinderId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsSeenByOwner = table.Column<bool>(type: "boolean", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostReportFoundClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LostReportFoundClaims_AppUsers_FinderId",
                        column: x => x.FinderId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LostReportFoundClaims_LostReports_LostReportId",
                        column: x => x.LostReportId,
                        principalTable: "LostReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LostReportFoundClaims_FinderId",
                table: "LostReportFoundClaims",
                column: "FinderId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReportFoundClaims_LostReportId_CreatedAt",
                table: "LostReportFoundClaims",
                columns: new[] { "LostReportId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LostReportFoundClaims_LostReportId_FinderId",
                table: "LostReportFoundClaims",
                columns: new[] { "LostReportId", "FinderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LostReportFoundClaims");
        }
    }
}
