using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zaster.Migrations
{
    /// <inheritdoc />
    public partial class NotificationDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationDevices",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationDevices",
                table: "Users");
        }
    }
}
