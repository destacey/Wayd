using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedStatusWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStatuses_WorkflowId_Name",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses");

            migrationBuilder.AddColumn<string>(
                name: "ExternalKey",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetired",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExternalKey",
                schema: "StatusWorkflows",
                table: "StatusWorkflows",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ownership",
                schema: "StatusWorkflows",
                table: "StatusWorkflows",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Owned");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_WorkflowId_ExternalKey",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses",
                columns: new[] { "WorkflowId", "ExternalKey" },
                unique: true,
                filter: "[ExternalKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_WorkflowId_Name",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses",
                columns: new[] { "WorkflowId", "Name" },
                unique: true,
                filter: "[IsRetired] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StatusWorkflows_OwnerType_ExternalKey",
                schema: "StatusWorkflows",
                table: "StatusWorkflows",
                columns: new[] { "OwnerType", "ExternalKey" },
                unique: true,
                filter: "[ExternalKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowStatuses_WorkflowId_ExternalKey",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStatuses_WorkflowId_Name",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses");

            migrationBuilder.DropIndex(
                name: "IX_StatusWorkflows_OwnerType_ExternalKey",
                schema: "StatusWorkflows",
                table: "StatusWorkflows");

            migrationBuilder.DropColumn(
                name: "ExternalKey",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses");

            migrationBuilder.DropColumn(
                name: "IsRetired",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses");

            migrationBuilder.DropColumn(
                name: "ExternalKey",
                schema: "StatusWorkflows",
                table: "StatusWorkflows");

            migrationBuilder.DropColumn(
                name: "Ownership",
                schema: "StatusWorkflows",
                table: "StatusWorkflows");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_WorkflowId_Name",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses",
                columns: new[] { "WorkflowId", "Name" },
                unique: true);
        }
    }
}
