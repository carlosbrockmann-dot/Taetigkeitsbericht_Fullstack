using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Taetigkeitsbericht.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mitarbeiter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Benutzername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswortHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mitarbeiter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "zeiteintrag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MitarbeiterId = table.Column<int>(type: "integer", nullable: false),
                    MandantId = table.Column<int>(type: "integer", nullable: true),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    UhrzeitVon = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    UhrzeitBis = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    PauseBeginn = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    PauseEnde = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Pause2Beginn = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Pause2Ende = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Anmerkung = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zeiteintrag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_zeiteintrag_mitarbeiter_MitarbeiterId",
                        column: x => x.MitarbeiterId,
                        principalTable: "mitarbeiter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mitarbeiter_Benutzername",
                table: "mitarbeiter",
                column: "Benutzername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mitarbeiter_Email",
                table: "mitarbeiter",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zeiteintrag_MitarbeiterId_Datum",
                table: "zeiteintrag",
                columns: new[] { "MitarbeiterId", "Datum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zeiteintrag");

            migrationBuilder.DropTable(
                name: "mitarbeiter");
        }
    }
}
