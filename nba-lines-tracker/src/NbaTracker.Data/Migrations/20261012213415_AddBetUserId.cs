using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBetUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Bets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: every bet that existed before this migration was placed by the
            // (at the time, only) admin account. Must run BEFORE the foreign key below —
            // that constraint requires every UserId to match a real Users.Id, and 0 (the
            // placeholder EF defaulted every existing row to above) never does.
            migrationBuilder.Sql(@"
                UPDATE ""Bets""
                SET ""UserId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""IsAdmin"" = true ORDER BY ""Id"" LIMIT 1)
                WHERE ""UserId"" = 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_UserId_Sport",
                table: "Bets",
                columns: new[] { "UserId", "Sport" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bets_Users_UserId",
                table: "Bets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bets_Users_UserId",
                table: "Bets");

            migrationBuilder.DropIndex(
                name: "IX_Bets_UserId_Sport",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Bets");
        }
    }
}
