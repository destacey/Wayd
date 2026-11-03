using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class FilterManagedNameIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStatuses_WorkflowId_Name",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_RecordTypes_OwnerType_Name",
                schema: "StatusWorkflows",
                table: "RecordTypes");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_WorkflowId_Name",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses",
                columns: new[] { "WorkflowId", "Name" },
                unique: true,
                filter: "[ExternalKey] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecordTypes_OwnerType_Name",
                schema: "StatusWorkflows",
                table: "RecordTypes",
                columns: new[] { "OwnerType", "Name" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStatuses_WorkflowId_Name",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_RecordTypes_OwnerType_Name",
                schema: "StatusWorkflows",
                table: "RecordTypes");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_WorkflowId_Name",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses",
                columns: new[] { "WorkflowId", "Name" },
                unique: true,
                filter: "[IsRetired] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RecordTypes_OwnerType_Name",
                schema: "StatusWorkflows",
                table: "RecordTypes",
                columns: new[] { "OwnerType", "Name" },
                unique: true);
        }
    }
}
