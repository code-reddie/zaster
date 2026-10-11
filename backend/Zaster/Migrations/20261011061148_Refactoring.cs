using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zaster.Migrations
{
    /// <inheritdoc />
    public partial class Refactoring : Migration
    {
        /// <summary>
        /// Kategorien und Regeln ziehen vom Nutzer auf das Konto um. Wie besprochen startet jedes Konto
        /// leer: alte Kategorien, Regeln und Zuordnungen werden gelöscht. Nutzer, Konten (mit PIN)
        /// und Buchungen bleiben; alle Buchungen sind danach offen („nicht geprüft“).
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Transactions SET CategoryId = NULL;");

            migrationBuilder.DropTable(
                name: "CategorizationRules");

            migrationBuilder.Sql("DELETE FROM Categories;");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "ParentCategoryId",
                table: "Categories",
                newName: "ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_ParentCategoryId",
                table: "Categories",
                newName: "IX_Categories_ParentId");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.RenameColumn(
                name: "Auftragsgeber",
                table: "Transactions",
                newName: "Auftraggeber");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Transactions",
                type: "TEXT",
                nullable: false,
                defaultValue: "Unbekannt");

            migrationBuilder.AddColumn<bool>(
                name: "Reviewed",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Transactions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Bisher DateTimeOffset („2026-05-04 00:00:00+00:00“), jetzt Datum („2026-05-04“).
            // CSV und FinTS haben Mitternacht UTC gespeichert, der Tag bleibt also derselbe.
            migrationBuilder.Sql("""
                UPDATE Transactions SET
                    CreatedAt = substr(Buchung, 1, 10) || ' 00:00:00',
                    Buchung = substr(Buchung, 1, 10),
                    Valuta = substr(Valuta, 1, 10);
                """);

            migrationBuilder.DropIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId_Buchung",
                table: "Transactions",
                columns: new[] { "AccountId", "Buchung" });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", nullable: false),
                    Field = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rules_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_AccountId",
                table: "Categories",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_AccountId",
                table: "Rules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_CategoryId",
                table: "Rules",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Accounts_AccountId",
                table: "Categories",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentId",
                table: "Categories",
                column: "ParentId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Die alten Kategorien und Regeln sind gelöscht, ein Zurück gibt es nicht. Vorher /data sichern.
            throw new NotSupportedException("Die Migration Refactoring lässt sich nicht rückgängig machen.");
        }
    }
}
