using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddReplicaWatermarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing copies start with "{}", no watermark on any group, so the next event for each applies
            // as it did before this change. An empty string would not deserialize.
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "Work",
                table: "WorkTeams",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Watermarks",
                schema: "Work",
                table: "WorkTeams",
                type: "varchar(1024)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "Work",
                table: "WorkProjects",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Watermarks",
                schema: "Work",
                table: "WorkProjects",
                type: "varchar(1024)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "Work",
                table: "WorkIterations",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Watermarks",
                schema: "Work",
                table: "WorkIterations",
                type: "varchar(1024)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "Ppm",
                table: "StrategicThemes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Watermarks",
                schema: "Ppm",
                table: "StrategicThemes",
                type: "varchar(1024)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "Ppm",
                table: "PpmTeams",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Watermarks",
                schema: "Ppm",
                table: "PpmTeams",
                type: "varchar(1024)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "Planning",
                table: "PlanningTeams",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Watermarks",
                schema: "Planning",
                table: "PlanningTeams",
                type: "varchar(1024)",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Work",
                table: "WorkTeams");

            migrationBuilder.DropColumn(
                name: "Watermarks",
                schema: "Work",
                table: "WorkTeams");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Work",
                table: "WorkProjects");

            migrationBuilder.DropColumn(
                name: "Watermarks",
                schema: "Work",
                table: "WorkProjects");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Work",
                table: "WorkIterations");

            migrationBuilder.DropColumn(
                name: "Watermarks",
                schema: "Work",
                table: "WorkIterations");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Ppm",
                table: "StrategicThemes");

            migrationBuilder.DropColumn(
                name: "Watermarks",
                schema: "Ppm",
                table: "StrategicThemes");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Ppm",
                table: "PpmTeams");

            migrationBuilder.DropColumn(
                name: "Watermarks",
                schema: "Ppm",
                table: "PpmTeams");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Planning",
                table: "PlanningTeams");

            migrationBuilder.DropColumn(
                name: "Watermarks",
                schema: "Planning",
                table: "PlanningTeams");
        }
    }
}
