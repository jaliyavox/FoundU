using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoundReportMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FoundReportMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundReportMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundReportMessages_AppUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundReportMessages_AppUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundReportMessages_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportMessages_FoundReportId_CreatedAt",
                table: "FoundReportMessages",
                columns: new[] { "FoundReportId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportMessages_FoundReportId_SenderId",
                table: "FoundReportMessages",
                columns: new[] { "FoundReportId", "SenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportMessages_RecipientId",
                table: "FoundReportMessages",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportMessages_SenderId",
                table: "FoundReportMessages",
                column: "SenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FoundReportMessages");
        }
    }
}
