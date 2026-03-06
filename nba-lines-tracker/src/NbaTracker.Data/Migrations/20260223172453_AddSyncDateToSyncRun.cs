using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NbaTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncDateToSyncRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "SyncDate",
                table: "SyncRuns",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncDate",
                table: "SyncRuns");
        }
    }
}
