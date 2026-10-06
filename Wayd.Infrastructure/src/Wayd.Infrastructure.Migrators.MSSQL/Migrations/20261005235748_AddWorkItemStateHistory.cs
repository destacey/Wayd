using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemStateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkItemHistoryWatermark",
                schema: "Work",
                table: "Workspaces",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkItemStateHistory",
                schema: "Work",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IterationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalIterationId = table.Column<int>(type: "int", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    StatusName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    WorkTypeId = table.Column<int>(type: "int", nullable: true),
                    WorkTypeName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TeamKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedToExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StoryPoints = table.Column<double>(type: "float", nullable: true),
                    Effort = table.Column<double>(type: "float", nullable: true),
                    Size = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemStateHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemStateHistory_Employees_AssignedToId",
                        column: x => x.AssignedToId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkItemStateHistory_Iterations_IterationId",
                        column: x => x.IterationId,
                        principalSchema: "Work",
                        principalTable: "Iterations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkItemStateHistory_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "Work",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemStateHistory_WorkStatuses_StatusId",
                        column: x => x.StatusId,
                        principalSchema: "Work",
                        principalTable: "WorkStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemStateHistory_WorkTypes_WorkTypeId",
                        column: x => x.WorkTypeId,
                        principalSchema: "Work",
                        principalTable: "WorkTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemStateHistory_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "Work",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_AssignedToExternalId",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "AssignedToExternalId",
                filter: "[AssignedToExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_AssignedToId",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_IterationId_ValidFrom",
                schema: "Work",
                table: "WorkItemStateHistory",
                columns: new[] { "IterationId", "ValidFrom" },
                filter: "[IterationId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "WorkItemId", "ValidTo", "StatusCategory", "WorkTypeId", "StoryPoints", "Effort", "Size" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_StatusId",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_WorkItemId_Open",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "WorkItemId",
                unique: true,
                filter: "[ValidTo] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_WorkItemId_Revision",
                schema: "Work",
                table: "WorkItemStateHistory",
                columns: new[] { "WorkItemId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_WorkspaceId",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_WorkTypeId",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "WorkTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemStateHistory",
                schema: "Work");

            migrationBuilder.DropColumn(
                name: "WorkItemHistoryWatermark",
                schema: "Work",
                table: "Workspaces");
        }
    }
}
