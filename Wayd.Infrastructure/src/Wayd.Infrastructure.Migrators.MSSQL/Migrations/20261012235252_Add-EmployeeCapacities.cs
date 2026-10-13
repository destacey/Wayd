using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCapacities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeCapacities",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCapacities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCapacities_Employees_Id",
                        column: x => x.Id,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTeamAllocations",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Start = table.Column<DateTime>(type: "date", nullable: false),
                    End = table.Column<DateTime>(type: "date", nullable: true),
                    Percent = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTeamAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeTeamAllocations_EmployeeCapacities_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "EmployeeCapacities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeTeamAllocations_TeamMemberRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "Organization",
                        principalTable: "TeamMemberRoles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeTeamAllocations_Teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Organization",
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTeamAllocations_EmployeeId_Start",
                schema: "Organization",
                table: "EmployeeTeamAllocations",
                columns: new[] { "EmployeeId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTeamAllocations_RoleId",
                schema: "Organization",
                table: "EmployeeTeamAllocations",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTeamAllocations_TeamId_Start",
                schema: "Organization",
                table: "EmployeeTeamAllocations",
                columns: new[] { "TeamId", "Start" })
                .Annotation("SqlServer:Include", new[] { "EmployeeId", "RoleId", "End", "Percent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeTeamAllocations",
                schema: "Organization");

            migrationBuilder.DropTable(
                name: "EmployeeCapacities",
                schema: "Organization");
        }
    }
}
