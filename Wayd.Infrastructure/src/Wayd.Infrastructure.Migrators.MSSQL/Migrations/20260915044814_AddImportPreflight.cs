using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddImportPreflight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppliedImportProcessId",
                schema: "Imports",
                table: "ImportProcesses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreflight",
                schema: "Imports",
                table: "ImportProcesses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedImportProcessId",
                schema: "Imports",
                table: "ImportProcesses");

            migrationBuilder.DropColumn(
                name: "IsPreflight",
                schema: "Imports",
                table: "ImportProcesses");
        }
    }
}
