using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Server.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddContentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    JellyfinItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NotificationCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsAdminViewed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentRequests_IsAdminViewed",
                table: "ContentRequests",
                column: "IsAdminViewed");

            migrationBuilder.CreateIndex(
                name: "IX_ContentRequests_NormalizedTitle",
                table: "ContentRequests",
                column: "NormalizedTitle");

            migrationBuilder.CreateIndex(
                name: "IX_ContentRequests_Status",
                table: "ContentRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContentRequests_UserId",
                table: "ContentRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentRequests_UserId_Type_Status",
                table: "ContentRequests",
                columns: new[] { "UserId", "Type", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentRequests");
        }
    }
}
