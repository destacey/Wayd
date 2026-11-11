using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class SplitIncidentDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                schema: "ProductManagement",
                table: "Incidents",
                newName: "Symptoms");

            migrationBuilder.AddColumn<string>(
                name: "RootCause",
                schema: "ProductManagement",
                table: "Incidents",
                type: "nvarchar(max)",
                maxLength: 4096,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RootCause",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.RenameColumn(
                name: "Symptoms",
                schema: "ProductManagement",
                table: "Incidents",
                newName: "Description");
        }
    }
}
