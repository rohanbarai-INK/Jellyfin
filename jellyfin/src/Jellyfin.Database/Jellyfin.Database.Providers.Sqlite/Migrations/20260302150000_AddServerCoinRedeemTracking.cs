using Jellyfin.Database.Implementations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations
{
    /// <summary>
    /// Tracks server-side coin deductions for quota top-up requests.
    /// </summary>
    [DbContext(typeof(JellyfinDbContext))]
    [Migration("20260302150000_AddServerCoinRedeemTracking")]
    public partial class AddServerCoinRedeemTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoinRedeemCost",
                table: "ContentRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ContentRequests_UserId_CoinRedeemCost",
                table: "ContentRequests",
                columns: new[] { "UserId", "CoinRedeemCost" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentRequests_UserId_CoinRedeemCost",
                table: "ContentRequests");

            migrationBuilder.DropColumn(
                name: "CoinRedeemCost",
                table: "ContentRequests");
        }
    }
}
