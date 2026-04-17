using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSeasonStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSeasonStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeasonYear = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalXp = table.Column<long>(type: "INTEGER", nullable: false),
                    AchievementXp = table.Column<long>(type: "INTEGER", nullable: false),
                    AchievementCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSeasonStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSeasonStats_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSeasonStats_UserId_SeasonYear",
                table: "UserSeasonStats",
                columns: new[] { "UserId", "SeasonYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSeasonStats_SeasonYear_TotalXp",
                table: "UserSeasonStats",
                columns: new[] { "SeasonYear", "TotalXp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserSeasonStats");
        }
    }
}
