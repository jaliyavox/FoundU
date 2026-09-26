using System;
using FoundU.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations;

[DbContext(typeof(FoundUDbContext))]
[Migration("20260926140000_AddDeviceRegistrations")]
public partial class AddDeviceRegistrations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeviceRegistrations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                FcmToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeactivatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeviceRegistrations", x => x.Id);
                table.ForeignKey(
                    name: "FK_DeviceRegistrations_AppUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeviceRegistrations_FcmToken",
            table: "DeviceRegistrations",
            column: "FcmToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeviceRegistrations_UserId_IsActive",
            table: "DeviceRegistrations",
            columns: new[] { "UserId", "IsActive" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "DeviceRegistrations");
}
