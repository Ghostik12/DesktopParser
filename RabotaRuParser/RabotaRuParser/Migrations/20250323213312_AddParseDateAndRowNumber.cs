using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RabotaRuParser.Migrations
{
    /// <inheritdoc />
    public partial class AddParseDateAndRowNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ParseDate",
                table: "Vacancies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RowNumber",
                table: "Vacancies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParseDate",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "RowNumber",
                table: "Vacancies");
        }
    }
}
