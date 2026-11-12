using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentTeamsChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TeamsChatCreatedByWayd",
                schema: "ProductManagement",
                table: "Incidents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TeamsChatDisplayName",
                schema: "ProductManagement",
                table: "Incidents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsChatId",
                schema: "ProductManagement",
                table: "Incidents",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsChatLink",
                schema: "ProductManagement",
                table: "Incidents",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamsChatCreatedByWayd",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "TeamsChatDisplayName",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "TeamsChatId",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "TeamsChatLink",
                schema: "ProductManagement",
                table: "Incidents");
        }
    }
}
