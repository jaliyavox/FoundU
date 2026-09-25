using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchSuggestionStaffNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StaffNote",
                table: "MatchSuggestions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaffNote",
                table: "MatchSuggestions");
        }
    }
}
