using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zaster.Migrations
{
    /// <inheritdoc />
    public partial class AccountLastSyncError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "Accounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "Accounts");
        }
    }
}
