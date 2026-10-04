using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIterationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanningSprints_TeamId",
                schema: "Planning",
                table: "PlanningSprints");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "Planning",
                table: "PlanningSprints");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "Work",
                table: "Iterations");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningSprints_TeamId",
                schema: "Planning",
                table: "PlanningSprints",
                column: "TeamId")
                .Annotation("SqlServer:Include", new[] { "Key", "Name", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlanningSprints_TeamId",
                schema: "Planning",
                table: "PlanningSprints");

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "Planning",
                table: "PlanningSprints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "Work",
                table: "Iterations",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningSprints_TeamId",
                schema: "Planning",
                table: "PlanningSprints",
                column: "TeamId")
                .Annotation("SqlServer:Include", new[] { "Key", "Name", "Type", "State" });
        }
    }
}
