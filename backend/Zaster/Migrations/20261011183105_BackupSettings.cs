using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zaster.Migrations
{
    /// <inheritdoc />
    public partial class BackupSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Time = table.Column<string>(type: "TEXT", nullable: true),
                    LastBackupAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BackupSettings",
                columns: new[] { "Id", "LastBackupAt", "LastError", "Time" },
                values: new object[] { 1, null, null, "03:00" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupSettings");
        }
    }
}
