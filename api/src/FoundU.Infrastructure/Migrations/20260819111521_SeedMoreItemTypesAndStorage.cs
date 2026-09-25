using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedMoreItemTypesAndStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ItemTypes",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "DeletedAt", "IsActive", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("51111111-1111-1111-1111-111111111119"), new Guid("21111111-1111-1111-1111-111111111113"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Jacket", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111111a"), new Guid("21111111-1111-1111-1111-111111111113"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Hoodie", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111111b"), new Guid("21111111-1111-1111-1111-111111111113"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Scarf", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111111c"), new Guid("21111111-1111-1111-1111-111111111113"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Cap", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111111d"), new Guid("21111111-1111-1111-1111-111111111113"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Umbrella", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111111e"), new Guid("21111111-1111-1111-1111-111111111114"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Student Card", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111111f"), new Guid("21111111-1111-1111-1111-111111111114"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "ID Card", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111120"), new Guid("21111111-1111-1111-1111-111111111114"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Bus Pass", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111121"), new Guid("21111111-1111-1111-1111-111111111114"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Bank Card", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111122"), new Guid("21111111-1111-1111-1111-111111111115"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "House Keys", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111123"), new Guid("21111111-1111-1111-1111-111111111115"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Car Keys", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111124"), new Guid("21111111-1111-1111-1111-111111111115"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Locker Key", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111125"), new Guid("21111111-1111-1111-1111-111111111116"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Watch", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111126"), new Guid("21111111-1111-1111-1111-111111111116"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Glasses", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111127"), new Guid("21111111-1111-1111-1111-111111111116"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Ring", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111128"), new Guid("21111111-1111-1111-1111-111111111116"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Bracelet", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111129"), new Guid("21111111-1111-1111-1111-111111111117"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Textbook", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111112a"), new Guid("21111111-1111-1111-1111-111111111117"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Notebook", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111112b"), new Guid("21111111-1111-1111-1111-111111111117"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Calculator", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111112c"), new Guid("21111111-1111-1111-1111-111111111117"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Pencil Case", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111112d"), new Guid("21111111-1111-1111-1111-111111111118"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Water Bottle", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111112e"), new Guid("21111111-1111-1111-1111-111111111118"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Lunch Box", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-11111111112f"), new Guid("21111111-1111-1111-1111-111111111118"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Sports Equipment", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111130"), new Guid("21111111-1111-1111-1111-111111111118"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Charger", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "StorageLocations",
                columns: new[] { "Id", "Building", "Capacity", "CreatedAt", "DeletedAt", "IsActive", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("41111111-1111-1111-1111-111111111112"), "Building C", 80, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Library Front Desk", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("41111111-1111-1111-1111-111111111113"), "Sports Block", 60, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Sports Complex Office", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("41111111-1111-1111-1111-111111111114"), "Building A", 120, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Student Services", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111119"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111111a"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111111b"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111111c"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111111d"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111111e"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111111f"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111120"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111121"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111122"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111123"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111124"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111125"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111126"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111127"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111128"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111129"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111112a"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111112b"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111112c"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111112d"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111112e"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-11111111112f"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111130"));

            migrationBuilder.DeleteData(
                table: "StorageLocations",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-111111111112"));

            migrationBuilder.DeleteData(
                table: "StorageLocations",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-111111111113"));

            migrationBuilder.DeleteData(
                table: "StorageLocations",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-111111111114"));
        }
    }
}
