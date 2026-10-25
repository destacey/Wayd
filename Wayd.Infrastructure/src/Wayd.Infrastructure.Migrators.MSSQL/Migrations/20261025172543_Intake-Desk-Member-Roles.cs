using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class IntakeDeskMemberRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeRequests_Employees_AssignedReviewerId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropTable(
                name: "IntakeDeskReviewers",
                schema: "DemandManagement");

            migrationBuilder.RenameColumn(
                name: "AssignedReviewerId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                newName: "AssigneeId");

            migrationBuilder.RenameIndex(
                name: "IX_IntakeRequests_AssignedReviewerId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                newName: "IX_IntakeRequests_AssigneeId");

            migrationBuilder.CreateTable(
                name: "IntakeDeskMembers",
                schema: "DemandManagement",
                columns: table => new
                {
                    DeskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDeskMembers", x => new { x.DeskId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_IntakeDeskMembers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeDeskMembers_IntakeDesks_DeskId",
                        column: x => x.DeskId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeDesks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDeskMembers_EmployeeId",
                schema: "DemandManagement",
                table: "IntakeDeskMembers",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeRequests_Employees_AssigneeId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "AssigneeId",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeRequests_Employees_AssigneeId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropTable(
                name: "IntakeDeskMembers",
                schema: "DemandManagement");

            migrationBuilder.RenameColumn(
                name: "AssigneeId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                newName: "AssignedReviewerId");

            migrationBuilder.RenameIndex(
                name: "IX_IntakeRequests_AssigneeId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                newName: "IX_IntakeRequests_AssignedReviewerId");

            migrationBuilder.CreateTable(
                name: "IntakeDeskReviewers",
                schema: "DemandManagement",
                columns: table => new
                {
                    DeskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDeskReviewers", x => new { x.DeskId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_IntakeDeskReviewers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeDeskReviewers_IntakeDesks_DeskId",
                        column: x => x.DeskId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeDesks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDeskReviewers_EmployeeId",
                schema: "DemandManagement",
                table: "IntakeDeskReviewers",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeRequests_Employees_AssignedReviewerId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "AssignedReviewerId",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
