using System;
using Jellyfin.Database.Implementations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations
{
    /// <summary>
    /// Adds user achievement unlock persistence.
    /// </summary>
    [DbContext(typeof(JellyfinDbContext))]
    [Migration("20260302103000_AddUserAchievements")]
    public partial class AddUserAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AchievementId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UnlockedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievements_AchievementDefinition_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "AchievementDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_AchievementId",
                table: "UserAchievements",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_UnlockedAtUtc",
                table: "UserAchievements",
                columns: new[] { "UserId", "UnlockedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAchievements");
        }
    }
}
