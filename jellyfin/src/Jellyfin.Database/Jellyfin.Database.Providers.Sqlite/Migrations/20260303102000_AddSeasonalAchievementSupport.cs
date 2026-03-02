using System;
using System.Linq;
using Jellyfin.Database.Implementations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations
{
    /// <summary>
    /// Adds seasonal achievement cadence metadata and season-year user unlock tracking.
    /// </summary>
    [DbContext(typeof(JellyfinDbContext))]
    [Migration("20260303102000_AddSeasonalAchievementSupport")]
    public partial class AddSeasonalAchievementSupport : Migration
    {
        private static readonly string[] _seasonalAchievementIds =
        [
            "weekend-viewer",
            "night-owl",
            "early-bird",
            "double-feature",
            "binge-session",
            "mega-binge",
            "midnight-marathon",
            "weekend-marathon",
            "back-to-back",
            "one-sitting",
            "prime-time",
            "five-episodes",
            "ten-episodes",
            "movie-buff",
            "cinema-lover",
            "ten-hours",
            "fifty-hours",
            "genre-explorer",
            "balanced-viewer",
            "global-explorer",
            "international-film",
            "documentary-dive",
            "request-pioneer",
            "request-regular",
            "smart-spender",
            "high-roller",
            "loyal-redeemer",
            "strategic-planner"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeasonType",
                table: "AchievementDefinition",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements");

            migrationBuilder.AddColumn<int>(
                name: "SeasonYear",
                table: "UserAchievements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId_Permanent",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true,
                filter: "\"SeasonYear\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId_SeasonYear",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId", "SeasonYear" },
                unique: true,
                filter: "\"SeasonYear\" IS NOT NULL");

            migrationBuilder.Sql("UPDATE AchievementDefinition SET IsSeasonal = 0, SeasonType = NULL;");
            migrationBuilder.Sql(
                "UPDATE AchievementDefinition SET IsSeasonal = 1, SeasonType = 'yearly' WHERE Id IN "
                + BuildInClause(_seasonalAchievementIds)
                + ";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_UserId_AchievementId_Permanent",
                table: "UserAchievements");

            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_UserId_AchievementId_SeasonYear",
                table: "UserAchievements");

            migrationBuilder.DropColumn(
                name: "SeasonType",
                table: "AchievementDefinition");

            migrationBuilder.DropColumn(
                name: "SeasonYear",
                table: "UserAchievements");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);
        }

        private static string BuildInClause(string[] values)
            => "(" + string.Join(",", values.Select(value => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'")) + ")";
    }
}
