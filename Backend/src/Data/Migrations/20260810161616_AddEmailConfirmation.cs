using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taetigkeitsbericht.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailBestaetigt",
                table: "mitarbeiter",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmailBestaetigungsToken",
                table: "mitarbeiter",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailBestaetigungsTokenAblauf",
                table: "mitarbeiter",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailBestaetigt",
                table: "mitarbeiter");

            migrationBuilder.DropColumn(
                name: "EmailBestaetigungsToken",
                table: "mitarbeiter");

            migrationBuilder.DropColumn(
                name: "EmailBestaetigungsTokenAblauf",
                table: "mitarbeiter");
        }
    }
}
