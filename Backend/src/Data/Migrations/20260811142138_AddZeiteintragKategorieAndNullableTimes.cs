using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taetigkeitsbericht.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddZeiteintragKategorieAndNullableTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeOnly>(
                name: "UhrzeitVon",
                table: "zeiteintrag",
                type: "time without time zone",
                nullable: true,
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "UhrzeitBis",
                table: "zeiteintrag",
                type: "time without time zone",
                nullable: true,
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone");

            migrationBuilder.AddColumn<string>(
                name: "Kategorie",
                table: "zeiteintrag",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Arbeitstag");

            migrationBuilder.CreateIndex(
                name: "IX_zeiteintrag_MitarbeiterId_Kategorie_Datum",
                table: "zeiteintrag",
                columns: new[] { "MitarbeiterId", "Kategorie", "Datum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_zeiteintrag_MitarbeiterId_Kategorie_Datum",
                table: "zeiteintrag");

            migrationBuilder.DropColumn(
                name: "Kategorie",
                table: "zeiteintrag");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "UhrzeitVon",
                table: "zeiteintrag",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0),
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "UhrzeitBis",
                table: "zeiteintrag",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0),
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone",
                oldNullable: true);
        }
    }
}
