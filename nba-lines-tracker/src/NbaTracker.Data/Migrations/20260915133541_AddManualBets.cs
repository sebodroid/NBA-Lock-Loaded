using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddManualBets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManual",
                table: "Bets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MarketKey",
                table: "Bets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManual",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "MarketKey",
                table: "Bets");
        }
    }
}
