using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityCategoryAndOtherItemTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHighlighted",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111111"),
                column: "IsHighlighted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111112"),
                column: "IsHighlighted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111113"),
                column: "IsHighlighted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111114"),
                column: "IsHighlighted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111115"),
                column: "IsHighlighted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111116"),
                column: "IsHighlighted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111117"),
                column: "IsHighlighted",
                value: false);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111118"),
                column: "IsHighlighted",
                value: false);

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "Description", "IsActive", "IsDeleted", "IsHighlighted", "Name", "UpdatedAt" },
                values: new object[] { new Guid("21111111-1111-1111-1111-111111111119"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "University ID, national ID, driving licence, passport", true, false, true, "ID & Licences", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "ItemTypes",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "DeletedAt", "IsActive", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("51111111-1111-1111-1111-111111111141"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111142"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111143"), new Guid("21111111-1111-1111-1111-111111111113"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111144"), new Guid("21111111-1111-1111-1111-111111111114"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111145"), new Guid("21111111-1111-1111-1111-111111111115"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111146"), new Guid("21111111-1111-1111-1111-111111111116"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111147"), new Guid("21111111-1111-1111-1111-111111111117"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111148"), new Guid("21111111-1111-1111-1111-111111111118"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111131"), new Guid("21111111-1111-1111-1111-111111111119"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "University ID", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111132"), new Guid("21111111-1111-1111-1111-111111111119"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "National ID", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111133"), new Guid("21111111-1111-1111-1111-111111111119"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Driving Licence", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111134"), new Guid("21111111-1111-1111-1111-111111111119"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Passport", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111149"), new Guid("21111111-1111-1111-1111-111111111119"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Other", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111131"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111132"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111133"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111134"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111141"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111142"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111143"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111144"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111145"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111146"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111147"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111148"));

            migrationBuilder.DeleteData(
                table: "ItemTypes",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-111111111149"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-111111111119"));

            migrationBuilder.DropColumn(
                name: "IsHighlighted",
                table: "Categories");
        }
    }
}
