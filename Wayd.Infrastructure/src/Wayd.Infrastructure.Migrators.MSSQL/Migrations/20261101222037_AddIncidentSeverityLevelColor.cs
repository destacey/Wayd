using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentSeverityLevelColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "ProductManagement",
                table: "IncidentSeverityLevels",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "#8E9E98");

            migrationBuilder.Sql(
                """
                UPDATE [ProductManagement].[IncidentSeverityLevels]
                SET [Color] = CASE [SortOrder]
                    WHEN 0 THEN '#E07A4C'
                    WHEN 1 THEN '#D4A017'
                    WHEN 2 THEN '#7FA3BF'
                    ELSE '#8E9E98'
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                schema: "ProductManagement",
                table: "IncidentSeverityLevels");
        }
    }
}
