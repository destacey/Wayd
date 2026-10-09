using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintTeamDaysOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeamDaysOff",
                schema: "Work",
                table: "Iterations",
                type: "varchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamDaysOff",
                schema: "Work",
                table: "Iterations");
        }
    }
}
