using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zaster.Migrations
{
    /// <inheritdoc />
    public partial class RuleConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Field = table.Column<string>(type: "TEXT", nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleConditions_Rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuleConditions_RuleId",
                table: "RuleConditions",
                column: "RuleId");

            // Bisherige Regeln werden zu Regeln mit genau einer Bedingung.
            migrationBuilder.Sql("""
                INSERT INTO "RuleConditions" ("RuleId", "Field", "Pattern")
                SELECT "Id", "Field", "Pattern" FROM "Rules";
                """);

            migrationBuilder.DropColumn(
                name: "Field",
                table: "Rules");

            migrationBuilder.DropColumn(
                name: "Pattern",
                table: "Rules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Field",
                table: "Rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Pattern",
                table: "Rules",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Zurück bleibt nur die erste Bedingung jeder Regel.
            migrationBuilder.Sql("""
                UPDATE "Rules" SET
                    "Field" = (SELECT "Field" FROM "RuleConditions" c WHERE c."RuleId" = "Rules"."Id" ORDER BY c."Id" LIMIT 1),
                    "Pattern" = (SELECT "Pattern" FROM "RuleConditions" c WHERE c."RuleId" = "Rules"."Id" ORDER BY c."Id" LIMIT 1)
                WHERE EXISTS (SELECT 1 FROM "RuleConditions" c WHERE c."RuleId" = "Rules"."Id");
                """);

            migrationBuilder.DropTable(
                name: "RuleConditions");
        }
    }
}
