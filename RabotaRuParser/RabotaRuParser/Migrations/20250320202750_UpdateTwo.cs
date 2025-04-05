using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RabotaRuParser.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTwo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Employment",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "Experience",
                table: "Vacancies");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "Vacancies",
                newName: "SiteId");

            migrationBuilder.RenameColumn(
                name: "SiteName",
                table: "Vacancies",
                newName: "Domain");

            migrationBuilder.RenameColumn(
                name: "Schedule",
                table: "Vacancies",
                newName: "ContactName");

            migrationBuilder.RenameColumn(
                name: "Salary",
                table: "Vacancies",
                newName: "Address");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SiteId",
                table: "Vacancies",
                newName: "Source");

            migrationBuilder.RenameColumn(
                name: "Domain",
                table: "Vacancies",
                newName: "SiteName");

            migrationBuilder.RenameColumn(
                name: "ContactName",
                table: "Vacancies",
                newName: "Schedule");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Vacancies",
                newName: "Salary");

            migrationBuilder.AddColumn<string>(
                name: "Employment",
                table: "Vacancies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Experience",
                table: "Vacancies",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
