using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zaster.Migrations
{
    /// <inheritdoc />
    public partial class TradeRepublicFelder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Anlageklasse",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Art",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Devisenkurs",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExterneId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fremdbetrag",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fremdwaehrung",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Gebuehr",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GegenIban",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Isin",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Kurs",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mcc",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Steuer",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Stueck",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Zeitpunkt",
                table: "Transactions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Anlageklasse",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Art",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Devisenkurs",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExterneId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Fremdbetrag",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Fremdwaehrung",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Gebuehr",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "GegenIban",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Isin",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Kurs",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Mcc",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Steuer",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Stueck",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Zeitpunkt",
                table: "Transactions");
        }
    }
}
