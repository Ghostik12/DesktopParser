using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RabotaRuParser.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRowNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowNumber",
                table: "Vacancies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RowNumber",
                table: "Vacancies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
