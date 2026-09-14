using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPropLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerPropLines",
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
                    LineTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPropLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerPropLines_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerPropLines_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPropLines_GameId_PlayerId_MarketKey",
                table: "PlayerPropLines",
                columns: new[] { "GameId", "PlayerId", "MarketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPropLines_PlayerId",
                table: "PlayerPropLines",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerPropLines");
        }
    }
}
