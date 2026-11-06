using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPropLineHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerPropLineHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    MarketKey = table.Column<string>(type: "text", nullable: false),
                    Line = table.Column<decimal>(type: "numeric", nullable: false),
                    OverOdds = table.Column<int>(type: "integer", nullable: true),
                    UnderOdds = table.Column<int>(type: "integer", nullable: true),
                    Bookmaker = table.Column<string>(type: "text", nullable: false),
                    UnderBookmaker = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPropLineHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerPropLineHistories_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerPropLineHistories_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPropLineHistories_GameId_PlayerId_MarketKey_RecordedAt",
                table: "PlayerPropLineHistories",
                columns: new[] { "GameId", "PlayerId", "MarketKey", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPropLineHistories_PlayerId",
                table: "PlayerPropLineHistories",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerPropLineHistories");
        }
    }
}
