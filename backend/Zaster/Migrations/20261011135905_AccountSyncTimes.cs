using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zaster.Migrations
{
    /// <inheritdoc />
    public partial class AccountSyncTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bestehende Konten behalten den bisherigen nächtlichen Abruf um 04:00.
            migrationBuilder.AddColumn<string>(
                name: "SyncTimes",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "[\"04:00\"]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncTimes",
                table: "Accounts");
        }
    }
}
