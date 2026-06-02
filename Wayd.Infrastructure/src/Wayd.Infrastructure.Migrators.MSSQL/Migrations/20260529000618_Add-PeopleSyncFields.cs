using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddPeopleSyncFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Employees_Email",
            schema: "Organization",
            table: "Employees");

        migrationBuilder.AddColumn<string>(
            name: "EmployeeType",
            schema: "Organization",
            table: "Employees",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Employees_Email",
            schema: "Organization",
            table: "Employees",
            column: "Email",
            unique: true,
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Employees_Email",
            schema: "Organization",
            table: "Employees");

        migrationBuilder.DropColumn(
            name: "EmployeeType",
            schema: "Organization",
            table: "Employees");

        migrationBuilder.CreateIndex(
            name: "IX_Employees_Email",
            schema: "Organization",
            table: "Employees",
            column: "Email")
            .Annotation("SqlServer:Include", new[] { "Id" });
    }
}
