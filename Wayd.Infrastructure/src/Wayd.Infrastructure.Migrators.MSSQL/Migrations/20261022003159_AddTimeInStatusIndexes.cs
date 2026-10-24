using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeInStatusIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StatusTransitions_OwnerType_RecordId_ChangedOn",
                schema: "StatusWorkflows",
                table: "StatusTransitions");

            migrationBuilder.CreateIndex(
                name: "IX_StatusTransitions_OwnerType_ChangedOn",
                schema: "StatusWorkflows",
                table: "StatusTransitions",
                columns: new[] { "OwnerType", "ChangedOn" })
                .Annotation("SqlServer:Include", new[] { "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_StatusTransitions_OwnerType_RecordId_ChangedOn",
                schema: "StatusWorkflows",
                table: "StatusTransitions",
                columns: new[] { "OwnerType", "RecordId", "ChangedOn" })
                .Annotation("SqlServer:Include", new[] { "ToStatusName", "ToCategory", "ToAlias", "ToStatusId", "Sequence", "WorkflowId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StatusTransitions_OwnerType_ChangedOn",
                schema: "StatusWorkflows",
                table: "StatusTransitions");

            migrationBuilder.DropIndex(
                name: "IX_StatusTransitions_OwnerType_RecordId_ChangedOn",
                schema: "StatusWorkflows",
                table: "StatusTransitions");

            migrationBuilder.CreateIndex(
                name: "IX_StatusTransitions_OwnerType_RecordId_ChangedOn",
                schema: "StatusWorkflows",
                table: "StatusTransitions",
                columns: new[] { "OwnerType", "RecordId", "ChangedOn" })
                .Annotation("SqlServer:Include", new[] { "ToStatusName", "ToCategory", "ToAlias" });
        }
    }
}
