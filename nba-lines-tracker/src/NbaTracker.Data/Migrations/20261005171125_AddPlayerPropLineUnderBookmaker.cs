using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPropLineUnderBookmaker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnderBookmaker",
                table: "PlayerPropLines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnderBookmaker",
                table: "PlayerPropLines");
        }
    }
}
