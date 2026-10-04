using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteClaimVerificationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustodyLocationId",
                table: "Claims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MatchSuggestionId",
                table: "Claims",
                type: "uuid",
                nullable: true);

            // Preserve existing claims and attach their canonical suggestion/custody records.
            migrationBuilder.Sql("""
                UPDATE "Claims" AS c SET "CustodyLocationId" = f."StorageLocationId"
                FROM "FoundReports" AS f WHERE f."Id" = c."FoundReportId";
                UPDATE "Claims" AS c SET "MatchSuggestionId" = m."Id"
                FROM "MatchSuggestions" AS m
                WHERE m."LostReportId" = c."LostReportId" AND m."FoundReportId" = c."FoundReportId";
                """);

            migrationBuilder.CreateTable(
                name: "FoundVerificationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FoundReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoundVerificationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoundVerificationEvidence_AppUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoundVerificationEvidence_FoundReports_FoundReportId",
                        column: x => x.FoundReportId,
                        principalTable: "FoundReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_CustodyLocationId",
                table: "Claims",
                column: "CustodyLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_MatchSuggestionId",
                table: "Claims",
                column: "MatchSuggestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_OneActivePair",
                table: "Claims",
                columns: new[] { "StudentId", "LostReportId", "FoundReportId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN ('Pending', 'WaitingForAnswer', 'UnderReview', 'RevisionRequested', 'ManualReviewRequired', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_FoundVerificationEvidence_FoundReportId",
                table: "FoundVerificationEvidence",
                column: "FoundReportId");

            migrationBuilder.CreateIndex(
                name: "IX_FoundVerificationEvidence_RecordedByUserId",
                table: "FoundVerificationEvidence",
                column: "RecordedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_MatchSuggestions_MatchSuggestionId",
                table: "Claims",
                column: "MatchSuggestionId",
                principalTable: "MatchSuggestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_StorageLocations_CustodyLocationId",
                table: "Claims",
                column: "CustodyLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Claims_MatchSuggestions_MatchSuggestionId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_StorageLocations_CustodyLocationId",
                table: "Claims");

            migrationBuilder.DropTable(
                name: "FoundVerificationEvidence");

            migrationBuilder.DropIndex(
                name: "IX_Claims_CustodyLocationId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_MatchSuggestionId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_OneActivePair",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "CustodyLocationId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "MatchSuggestionId",
                table: "Claims");
        }
    }
}
