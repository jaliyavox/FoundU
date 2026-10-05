using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoundU.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLostReportCreatedForClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreatedForClaim",
                table: "LostReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedForClaim",
                table: "LostReports");
        }
    }
}
