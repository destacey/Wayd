using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddSyncRuns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SyncRuns",
            schema: "AppIntegrations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectorType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                TriggerSource = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                SyncType = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                WorkspacesPlanned = table.Column<int>(type: "int", nullable: false),
                WorkspacesSucceeded = table.Column<int>(type: "int", nullable: false),
                WorkspacesFailed = table.Column<int>(type: "int", nullable: false),
                WorkItemsProcessed = table.Column<int>(type: "int", nullable: false),
                ErrorsCount = table.Column<int>(type: "int", nullable: false),
                ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SyncRuns", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SyncRuns_ConnectionId",
            schema: "AppIntegrations",
            table: "SyncRuns",
            column: "ConnectionId");

        migrationBuilder.CreateIndex(
            name: "IX_SyncRuns_ConnectionId_StartedAt",
            schema: "AppIntegrations",
            table: "SyncRuns",
            columns: new[] { "ConnectionId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_SyncRuns_Status_StartedAt",
            schema: "AppIntegrations",
            table: "SyncRuns",
            columns: new[] { "Status", "StartedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SyncRuns",
            schema: "AppIntegrations");
    }
}
