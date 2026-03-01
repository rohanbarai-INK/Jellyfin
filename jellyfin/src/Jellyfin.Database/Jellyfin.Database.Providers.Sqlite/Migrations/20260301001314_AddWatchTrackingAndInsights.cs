using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchTrackingAndInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserBingeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeriesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWatchTicks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBingeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGenrePeriodStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodType = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    GenreId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TotalValidatedTicks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGenrePeriodStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPeriodHourlyStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodType = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Hour = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalValidatedTicks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPeriodHourlyStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPeriodStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodType = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodKey = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalValidatedTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    SessionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedMovies = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedEpisodes = table.Column<int>(type: "INTEGER", nullable: false),
                    BingeSessions = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPeriodStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWatchSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccumulatedTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    ValidatedTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    PlaybackSpeed = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0),
                    IsValidSession = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    SuspicionScore = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWatchSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserBingeSessions_UserId_SessionDateUtc",
                table: "UserBingeSessions",
                columns: new[] { "UserId", "SessionDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGenrePeriodStats_UserId_PeriodType_PeriodKey_GenreId",
                table: "UserGenrePeriodStats",
                columns: new[] { "UserId", "PeriodType", "PeriodKey", "GenreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPeriodHourlyStats_UserId_PeriodType_PeriodKey_Hour",
                table: "UserPeriodHourlyStats",
                columns: new[] { "UserId", "PeriodType", "PeriodKey", "Hour" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPeriodStats_UserId_PeriodType_PeriodKey",
                table: "UserPeriodStats",
                columns: new[] { "UserId", "PeriodType", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWatchSessions_ItemId_StartTimeUtc",
                table: "UserWatchSessions",
                columns: new[] { "ItemId", "StartTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWatchSessions_SessionId",
                table: "UserWatchSessions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWatchSessions_UserId_StartTimeUtc",
                table: "UserWatchSessions",
                columns: new[] { "UserId", "StartTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBingeSessions");

            migrationBuilder.DropTable(
                name: "UserGenrePeriodStats");

            migrationBuilder.DropTable(
                name: "UserPeriodHourlyStats");

            migrationBuilder.DropTable(
                name: "UserPeriodStats");

            migrationBuilder.DropTable(
                name: "UserWatchSessions");
        }
    }
}
