using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemEffortAndSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Effort",
                schema: "Work",
                table: "WorkItems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Size",
                schema: "Work",
                table: "WorkItems",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Effort",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Size",
                schema: "Work",
                table: "WorkItems");
        }
    }
}
