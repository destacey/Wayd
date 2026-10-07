using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemSourceRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemRevisionFills",
                schema: "Work",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HighestRevision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemRevisionFills", x => x.WorkItemId);
                    table.ForeignKey(
                        name: "FK_WorkItemRevisionFills_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "Work",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemSourceRevisions",
                schema: "Work",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Changed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExternalIterationId = table.Column<int>(type: "int", nullable: true),
                    StatusName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WorkTypeName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TeamKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AssignedToExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StoryPoints = table.Column<double>(type: "float", nullable: true),
                    Effort = table.Column<double>(type: "float", nullable: true),
                    Size = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemSourceRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemSourceRevisions_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "Work",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemSourceRevisions_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "Work",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemSourceRevisions_WorkItemId_Revision",
                schema: "Work",
                table: "WorkItemSourceRevisions",
                columns: new[] { "WorkItemId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemSourceRevisions_WorkspaceId",
                schema: "Work",
                table: "WorkItemSourceRevisions",
                column: "WorkspaceId");

            // Periods stored so far have no revisions behind them, so they could be neither extended
            // nor rebuilt. Dropping them and clearing the watermarks makes the next sync of either kind
            // read every revision from the start and rebuild the history from the log.
            migrationBuilder.Sql("DELETE FROM [Work].[WorkItemStateHistory];");
            migrationBuilder.Sql("UPDATE [Work].[Workspaces] SET [WorkItemHistoryWatermark] = NULL WHERE [WorkItemHistoryWatermark] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemRevisionFills",
                schema: "Work");

            migrationBuilder.DropTable(
                name: "WorkItemSourceRevisions",
                schema: "Work");
        }
    }
}
