using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessKeyCycleDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CycleEndDate",
                table: "AccessKeys",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CycleStartDate",
                table: "AccessKeys",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CycleEndDate",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "CycleStartDate",
                table: "AccessKeys");
        }
    }
}
