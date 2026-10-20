using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransitionMode",
                schema: "StatusWorkflows",
                table: "StatusWorkflows",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "AnyToAny");

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                schema: "StatusWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_StatusWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "StatusWorkflows",
                        principalTable: "StatusWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStatuses_FromStatusId",
                        column: x => x.FromStatusId,
                        principalSchema: "StatusWorkflows",
                        principalTable: "WorkflowStatuses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_WorkflowStatuses_ToStatusId",
                        column: x => x.ToStatusId,
                        principalSchema: "StatusWorkflows",
                        principalTable: "WorkflowStatuses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_FromStatusId",
                schema: "StatusWorkflows",
                table: "WorkflowTransitions",
                column: "FromStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_ToStatusId",
                schema: "StatusWorkflows",
                table: "WorkflowTransitions",
                column: "ToStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_WorkflowId_FromStatusId_ToStatusId",
                schema: "StatusWorkflows",
                table: "WorkflowTransitions",
                columns: new[] { "WorkflowId", "FromStatusId", "ToStatusId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowTransitions",
                schema: "StatusWorkflows");

            migrationBuilder.DropColumn(
                name: "TransitionMode",
                schema: "StatusWorkflows",
                table: "StatusWorkflows");
        }
    }
}
