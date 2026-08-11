using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taetigkeitsbericht.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "login_token",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MitarbeiterId = table.Column<int>(type: "integer", nullable: false),
                    Jti = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LaeuftAbAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WiderrufenAm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_token", x => x.Id);
                    table.ForeignKey(
                        name: "FK_login_token_mitarbeiter_MitarbeiterId",
                        column: x => x.MitarbeiterId,
                        principalTable: "mitarbeiter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_login_token_Jti",
                table: "login_token",
                column: "Jti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_login_token_MitarbeiterId_WiderrufenAm",
                table: "login_token",
                columns: new[] { "MitarbeiterId", "WiderrufenAm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_token");
        }
    }
}
