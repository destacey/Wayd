using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemExternalIdentityIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedToExternalId",
                schema: "Work",
                table: "WorkItemsExtended",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByExternalId",
                schema: "Work",
                table: "WorkItemsExtended",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedByExternalId",
                schema: "Work",
                table: "WorkItemsExtended",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemsExtended_AssignedToExternalId",
                schema: "Work",
                table: "WorkItemsExtended",
                column: "AssignedToExternalId",
                filter: "[AssignedToExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemsExtended_CreatedByExternalId",
                schema: "Work",
                table: "WorkItemsExtended",
                column: "CreatedByExternalId",
                filter: "[CreatedByExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemsExtended_LastModifiedByExternalId",
                schema: "Work",
                table: "WorkItemsExtended",
                column: "LastModifiedByExternalId",
                filter: "[LastModifiedByExternalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemsExtended_AssignedToExternalId",
                schema: "Work",
                table: "WorkItemsExtended");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemsExtended_CreatedByExternalId",
                schema: "Work",
                table: "WorkItemsExtended");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemsExtended_LastModifiedByExternalId",
                schema: "Work",
                table: "WorkItemsExtended");

            migrationBuilder.DropColumn(
                name: "AssignedToExternalId",
                schema: "Work",
                table: "WorkItemsExtended");

            migrationBuilder.DropColumn(
                name: "CreatedByExternalId",
                schema: "Work",
                table: "WorkItemsExtended");

            migrationBuilder.DropColumn(
                name: "LastModifiedByExternalId",
                schema: "Work",
                table: "WorkItemsExtended");
        }
    }
}
