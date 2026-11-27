using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccessEndDate",
                schema: "Organization",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Connector",
                schema: "Organization",
                table: "Employees",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                schema: "Organization",
                table: "Employees",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ownership",
                schema: "Organization",
                table: "Employees",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Owned");

            migrationBuilder.AddColumn<string>(
                name: "SystemId",
                schema: "Organization",
                table: "Employees",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessEndDate",
                schema: "Organization",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Connector",
                schema: "Organization",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                schema: "Organization",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Ownership",
                schema: "Organization",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SystemId",
                schema: "Organization",
                table: "Employees");
        }
    }
}
