using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LostReports_LastSeenAt",
                table: "LostReports");

            migrationBuilder.DropIndex(
                name: "IX_LostReports_Status_CategoryId_LastSeenLocationId",
                table: "LostReports");

            migrationBuilder.DropIndex(
                name: "IX_FoundReports_Status_CategoryId_FoundLocationId",
                table: "FoundReports");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalDecisions_ClaimId",
                table: "ApprovalDecisions");

            migrationBuilder.DeleteData(
                table: "AppUsers",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.RenameColumn(
                name: "LastSeenAt",
                table: "LostReports",
                newName: "EstimatedLostToAt");

            migrationBuilder.RenameColumn(
                name: "PublicDescription",
                table: "FoundReports",
                newName: "GeneralDescription");

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedLostFromAt",
                table: "LostReports",
                type: "timestamptz",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "ItemTypeId",
                table: "LostReports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "PrivateVerificationDetails",
                table: "FoundReports",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemTypeId",
                table: "FoundReports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ObservedAttributesJson",
                table: "FoundReports",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateVerificationAttributesJson",
                table: "FoundReports",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "AppUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "FoundReportStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundReportStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundReportStatusHistories_AppUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FoundReportStatusHistories_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemTypes_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ItemTypes",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "DeletedAt", "IsActive", "IsDeleted", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("51111111-1111-1111-1111-111111111111"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Laptop", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111112"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Phone", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111113"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Headphones", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111114"), new Guid("21111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Earphones", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111115"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Backpack", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111116"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Laptop Bag", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111117"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Purse", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("51111111-1111-1111-1111-111111111118"), new Guid("21111111-1111-1111-1111-111111111112"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, false, "Wallet", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchSuggestions_MatchScore_Range",
                table: "MatchSuggestions",
                sql: "\"MatchScore\" >= 0 AND \"MatchScore\" <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_EstimatedLostFromAt_EstimatedLostToAt",
                table: "LostReports",
                columns: new[] { "EstimatedLostFromAt", "EstimatedLostToAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_ItemTypeId",
                table: "LostReports",
                column: "ItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_Status_CategoryId_ItemTypeId_LastSeenLocationId",
                table: "LostReports",
                columns: new[] { "Status", "CategoryId", "ItemTypeId", "LastSeenLocationId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LostReports_EstimatedLostRange",
                table: "LostReports",
                sql: "\"EstimatedLostFromAt\" <= \"EstimatedLostToAt\"");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_ItemTypeId",
                table: "FoundReports",
                column: "ItemTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_Status_CategoryId_ItemTypeId_FoundLocationId",
                table: "FoundReports",
                columns: new[] { "Status", "CategoryId", "ItemTypeId", "FoundLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_NormalizedEmail",
                table: "AppUsers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_StudentNumber_Unique",
                table: "AppUsers",
                column: "StudentNumber",
                unique: true,
                filter: "\"StudentNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_ClaimId",
                table: "ApprovalDecisions",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportStatusHistories_ChangedAt",
                table: "FoundReportStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportStatusHistories_ChangedByUserId",
                table: "FoundReportStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundReportStatusHistories_FoundReportId",
                table: "FoundReportStatusHistories",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTypes_CategoryId_Name",
                table: "ItemTypes",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FoundReports_ItemTypes_ItemTypeId",
                table: "FoundReports",
                column: "ItemTypeId",
                principalTable: "ItemTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LostReports_ItemTypes_ItemTypeId",
                table: "LostReports",
                column: "ItemTypeId",
                principalTable: "ItemTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoundReports_ItemTypes_ItemTypeId",
                table: "FoundReports");

            migrationBuilder.DropForeignKey(
                name: "FK_LostReports_ItemTypes_ItemTypeId",
                table: "LostReports");

            migrationBuilder.DropTable(
                name: "FoundReportStatusHistories");

            migrationBuilder.DropTable(
                name: "ItemTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchSuggestions_MatchScore_Range",
                table: "MatchSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_LostReports_EstimatedLostFromAt_EstimatedLostToAt",
                table: "LostReports");

            migrationBuilder.DropIndex(
                name: "IX_LostReports_ItemTypeId",
                table: "LostReports");

            migrationBuilder.DropIndex(
                name: "IX_LostReports_Status_CategoryId_ItemTypeId_LastSeenLocationId",
                table: "LostReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LostReports_EstimatedLostRange",
                table: "LostReports");

            migrationBuilder.DropIndex(
                name: "IX_FoundReports_ItemTypeId",
                table: "FoundReports");

            migrationBuilder.DropIndex(
                name: "IX_FoundReports_Status_CategoryId_ItemTypeId_FoundLocationId",
                table: "FoundReports");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_NormalizedEmail",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_StudentNumber_Unique",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalDecisions_ClaimId",
                table: "ApprovalDecisions");

            migrationBuilder.DropColumn(
                name: "EstimatedLostFromAt",
                table: "LostReports");

            migrationBuilder.DropColumn(
                name: "ItemTypeId",
                table: "LostReports");

            migrationBuilder.DropColumn(
                name: "ItemTypeId",
                table: "FoundReports");

            migrationBuilder.DropColumn(
                name: "ObservedAttributesJson",
                table: "FoundReports");

            migrationBuilder.DropColumn(
                name: "PrivateVerificationAttributesJson",
                table: "FoundReports");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "AppUsers");

            migrationBuilder.RenameColumn(
                name: "EstimatedLostToAt",
                table: "LostReports",
                newName: "LastSeenAt");

            migrationBuilder.RenameColumn(
                name: "GeneralDescription",
                table: "FoundReports",
                newName: "PublicDescription");

            migrationBuilder.AlterColumn<string>(
                name: "PrivateVerificationDetails",
                table: "FoundReports",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "AppUsers",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "Email", "FullName", "IsDeleted", "IsSuspended", "PasswordHash", "PhoneNumber", "Role", "StudentNumber", "SuspendedAt", "SuspendedByUserId", "SuspensionReason", "UpdatedAt" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "admin@foundu.university.edu", "FoundU Administrator", false, false, "$2a$11$K9x3yQFqZ8h5oQxWc0m9UuG7l1i6f2Hs0z3s0R9Zt0v2E9c1lYyDe", null, "Admin", null, null, null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_LastSeenAt",
                table: "LostReports",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_Status_CategoryId_LastSeenLocationId",
                table: "LostReports",
                columns: new[] { "Status", "CategoryId", "LastSeenLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FoundReports_Status_CategoryId_FoundLocationId",
                table: "FoundReports",
                columns: new[] { "Status", "CategoryId", "FoundLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_ClaimId",
                table: "ApprovalDecisions",
                column: "ClaimId",
                unique: true);
        }
    }
}
