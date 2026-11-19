using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentEmployeeForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CommanderEmployeeId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "CommanderEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ReportedByEmployeeId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "ReportedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_SubmittedByEmployeeId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "SubmittedByEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Employees_CommanderEmployeeId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "CommanderEmployeeId",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Employees_ReportedByEmployeeId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "ReportedByEmployeeId",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_Employees_SubmittedByEmployeeId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "SubmittedByEmployeeId",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Employees_CommanderEmployeeId",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Employees_ReportedByEmployeeId",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_Employees_SubmittedByEmployeeId",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_CommanderEmployeeId",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_ReportedByEmployeeId",
                schema: "ProductManagement",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_SubmittedByEmployeeId",
                schema: "ProductManagement",
                table: "Incidents");
        }
    }
}
