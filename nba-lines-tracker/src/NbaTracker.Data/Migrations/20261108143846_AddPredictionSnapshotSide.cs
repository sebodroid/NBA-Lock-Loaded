using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionSnapshotSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PredictionSnapshots_GameId_PlayerId_MarketKey",
                table: "PredictionSnapshots");

            // Every row captured before this migration was recorded under the Over-only
            // system, so backfilling the default to "Over" (not "") keeps existing rows
            // valid for the BetSide string-enum conversion instead of leaving them with a
            // value that doesn't match any enum member.
            migrationBuilder.AddColumn<string>(
                name: "Side",
                table: "PredictionSnapshots",
                type: "text",
                nullable: false,
                defaultValue: "Over");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionSnapshots_GameId_PlayerId_MarketKey_Side",
                table: "PredictionSnapshots",
                columns: new[] { "GameId", "PlayerId", "MarketKey", "Side" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PredictionSnapshots_GameId_PlayerId_MarketKey_Side",
                table: "PredictionSnapshots");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "PredictionSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionSnapshots_GameId_PlayerId_MarketKey",
                table: "PredictionSnapshots",
                columns: new[] { "GameId", "PlayerId", "MarketKey" },
                unique: true);
        }
    }
}
