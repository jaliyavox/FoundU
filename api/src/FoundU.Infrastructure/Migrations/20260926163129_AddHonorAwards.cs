using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHonorAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HonorAwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    LostReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HonorAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HonorAwards_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HonorAwards_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HonorAwards_LostReports_LostReportId",
                        column: x => x.LostReportId,
                        principalTable: "LostReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HonorAwards_FoundReportId",
                table: "HonorAwards",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_HonorAwards_LostReportId",
                table: "HonorAwards",
                column: "LostReportId");

            migrationBuilder.CreateIndex(
                name: "IX_HonorAwards_UserId_CreatedAt",
                table: "HonorAwards",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HonorAwards_UserId_Reason_FoundReportId",
                table: "HonorAwards",
                columns: new[] { "UserId", "Reason", "FoundReportId" },
                unique: true,
                filter: "\"FoundReportId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HonorAwards_UserId_Reason_LostReportId",
                table: "HonorAwards",
                columns: new[] { "UserId", "Reason", "LostReportId" },
                unique: true,
                filter: "\"LostReportId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HonorAwards");
        }
    }
}
