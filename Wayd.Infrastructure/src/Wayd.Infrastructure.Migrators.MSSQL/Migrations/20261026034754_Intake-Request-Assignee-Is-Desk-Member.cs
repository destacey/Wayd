using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class IntakeRequestAssigneeIsDeskMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_DeskId_AssigneeId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                columns: new[] { "DeskId", "AssigneeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeRequests_IntakeDeskMembers_DeskId_AssigneeId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                columns: new[] { "DeskId", "AssigneeId" },
                principalSchema: "DemandManagement",
                principalTable: "IntakeDeskMembers",
                principalColumns: new[] { "DeskId", "EmployeeId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeRequests_IntakeDeskMembers_DeskId_AssigneeId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropIndex(
                name: "IX_IntakeRequests_DeskId_AssigneeId",
                schema: "DemandManagement",
                table: "IntakeRequests");
        }
    }
}
