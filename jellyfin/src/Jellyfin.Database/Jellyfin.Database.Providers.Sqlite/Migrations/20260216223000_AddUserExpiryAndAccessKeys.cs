using System;
using Jellyfin.Database.Implementations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Server.Implementations.Migrations
{
    /// <summary>
    /// Adds user expiry and access key tables.
    /// </summary>
    [DbContext(typeof(JellyfinDbContext))]
    [Migration("20260216223000_AddUserExpiryAndAccessKeys")]
    public partial class AddUserExpiryAndAccessKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DurationMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRedeemed = table.Column<bool>(type: "INTEGER", nullable: false),
                    RedeemedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessKeys_Users_RedeemedByUserId",
                        column: x => x.RedeemedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessKeys_Key",
                table: "AccessKeys",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessKeys_RedeemedByUserId",
                table: "AccessKeys",
                column: "RedeemedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "Users");
        }
    }
}
