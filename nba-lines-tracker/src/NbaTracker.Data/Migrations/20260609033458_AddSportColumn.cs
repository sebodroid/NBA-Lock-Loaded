using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSportColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_NbaApiId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Games_NbaGameId",
                table: "Games");

            migrationBuilder.AddColumn<string>(
                name: "Sport",
                table: "Teams",
                type: "text",
                nullable: false,
                defaultValue: "NBA");      // was: defaultValue: ""

            migrationBuilder.AddColumn<string>(
                name: "Sport",
                table: "SyncRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sport",
                table: "Games",
                type: "text",
                nullable: false,
                defaultValue: "NBA");      // was: defaultValue: ""

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Sport_NbaApiId",
                table: "Teams",
                columns: new[] { "Sport", "NbaApiId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_Sport_NbaGameId",
                table: "Games",
                columns: new[] { "Sport", "NbaGameId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Sport_NbaApiId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Games_Sport_NbaGameId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Sport",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Sport",
                table: "SyncRuns");

            migrationBuilder.DropColumn(
                name: "Sport",
                table: "Games");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_NbaApiId",
                table: "Teams",
                column: "NbaApiId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_NbaGameId",
                table: "Games",
                column: "NbaGameId",
                unique: true);
        }
    }
}
