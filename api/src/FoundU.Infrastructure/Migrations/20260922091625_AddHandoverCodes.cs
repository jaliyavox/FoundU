using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoverCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandInCode",
                table: "LostReports",
                type: "character(6)",
                fixedLength: true,
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectedAt",
                table: "Claims",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionCode",
                table: "Claims",
                type: "character(6)",
                fixedLength: true,
                maxLength: 6,
                nullable: true);

            // Every existing report gets a code before the unique index lands. A loop in the
            // database rather than a formula, so two rows can never draw the same number.
            migrationBuilder.Sql("""
                DO $$
                DECLARE r RECORD; c TEXT;
                BEGIN
                  FOR r IN SELECT "Id" FROM "LostReports" WHERE "HandInCode" = '' LOOP
                    LOOP
                      c := lpad(floor(random() * 1000000)::int::text, 6, '0');
                      EXIT WHEN NOT EXISTS (SELECT 1 FROM "LostReports" WHERE "HandInCode" = c);
                    END LOOP;
                    UPDATE "LostReports" SET "HandInCode" = c WHERE "Id" = r."Id";
                  END LOOP;
                END $$;
                """);

            // Under the old model approval meant returned. Those claims are treated as
            // collected at the moment they were approved, so history and the analytics agree.
            migrationBuilder.Sql("""
                UPDATE "Claims" c
                SET "CollectedAt" = (
                  SELECT MAX(d."DecidedAt") FROM "ApprovalDecisions" d
                  WHERE d."ClaimId" = c."Id" AND d."Decision" = 'Approved')
                FROM "FoundReports" f
                WHERE f."Id" = c."FoundReportId"
                  AND c."Status" = 'Approved'
                  AND f."Status" = 'Returned'
                  AND c."CollectedAt" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LostReports_HandInCode",
                table: "LostReports",
                column: "HandInCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_CollectionCode",
                table: "Claims",
                column: "CollectionCode",
                filter: "\"CollectionCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LostReports_HandInCode",
                table: "LostReports");

            migrationBuilder.DropIndex(
                name: "IX_Claims_CollectionCode",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "HandInCode",
                table: "LostReports");

            migrationBuilder.DropColumn(
                name: "CollectedAt",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "CollectionCode",
                table: "Claims");
        }
    }
}
