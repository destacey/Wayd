using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddEventVersionToActivityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventVersion",
                schema: "App",
                table: "ActivityLogs",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "1.0");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_EmployeeId",
                schema: "App",
                table: "ActivityLogs",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Employees_EmployeeId",
                schema: "App",
                table: "ActivityLogs",
                column: "EmployeeId",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Employees_EmployeeId",
                schema: "App",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_EmployeeId",
                schema: "App",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "EventVersion",
                schema: "App",
                table: "ActivityLogs");
        }
    }
}
