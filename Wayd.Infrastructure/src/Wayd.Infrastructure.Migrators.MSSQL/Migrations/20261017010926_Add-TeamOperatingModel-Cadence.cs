using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamOperatingModelCadence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CadenceAnchor",
                schema: "Organization",
                table: "TeamOperatingModels",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CadenceWeeks",
                schema: "Organization",
                table: "TeamOperatingModels",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CadenceAnchor",
                schema: "Organization",
                table: "TeamOperatingModels");

            migrationBuilder.DropColumn(
                name: "CadenceWeeks",
                schema: "Organization",
                table: "TeamOperatingModels");
        }
    }
}
