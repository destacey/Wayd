using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDeliverableTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectDeliverableTasks",
                schema: "Ppm",
                columns: table => new
                {
                    ProjectDeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDeliverableTasks", x => new { x.ProjectDeliverableId, x.ProjectTaskId });
                    table.ForeignKey(
                        name: "FK_ProjectDeliverableTasks_ProjectDeliverables_ProjectDeliverableId",
                        column: x => x.ProjectDeliverableId,
                        principalSchema: "Ppm",
                        principalTable: "ProjectDeliverables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectDeliverableTasks_ProjectTasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalSchema: "Ppm",
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverableTasks_ProjectTaskId",
                schema: "Ppm",
                table: "ProjectDeliverableTasks",
                column: "ProjectTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDeliverableTasks",
                schema: "Ppm");
        }
    }
}
