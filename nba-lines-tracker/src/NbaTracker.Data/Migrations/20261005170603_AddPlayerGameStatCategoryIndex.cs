using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerGameStatCategoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameStats_Category",
                table: "PlayerGameStats",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerGameStats_Category",
                table: "PlayerGameStats");
        }
    }
}
