using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AssignWorkflowSchemesToScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowAssignments_StatusWorkflows_WorkflowId",
                schema: "StatusWorkflows",
                table: "WorkflowAssignments");

            migrationBuilder.RenameColumn(
                name: "WorkflowId",
                schema: "StatusWorkflows",
                table: "WorkflowAssignments",
                newName: "SchemeId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowAssignments_WorkflowId",
                schema: "StatusWorkflows",
                table: "WorkflowAssignments",
                newName: "IX_WorkflowAssignments_SchemeId");

            // The column still holds workflow ids. Every assigned workflow gets its implicit default-only
            // scheme, and each assignment is pointed at its workflow's.
            migrationBuilder.Sql("""
                INSERT INTO [StatusWorkflows].[WorkflowSchemes]
                    ([Id], [OwnerType], [Name], [Description], [Ownership], [ExternalKey], [IsImplicit], [DefaultWorkflowId],
                     [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy])
                SELECT NEWID(), w.[OwnerType], w.[Name], NULL, 'Owned', NULL, 1, w.[Id],
                       SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), NULL
                FROM [StatusWorkflows].[StatusWorkflows] w
                WHERE EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowAssignments] a WHERE a.[SchemeId] = w.[Id])
                  AND NOT EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowSchemes] s WHERE s.[IsImplicit] = 1 AND s.[DefaultWorkflowId] = w.[Id]);

                UPDATE a
                SET a.[SchemeId] = s.[Id]
                FROM [StatusWorkflows].[WorkflowAssignments] a
                JOIN [StatusWorkflows].[WorkflowSchemes] s ON s.[IsImplicit] = 1 AND s.[DefaultWorkflowId] = a.[SchemeId];
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowAssignments_WorkflowSchemes_SchemeId",
                schema: "StatusWorkflows",
                table: "WorkflowAssignments",
                column: "SchemeId",
                principalSchema: "StatusWorkflows",
                principalTable: "WorkflowSchemes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowAssignments_WorkflowSchemes_SchemeId",
                schema: "StatusWorkflows",
                table: "WorkflowAssignments");

            // Each assignment goes back to its scheme's default workflow. The schemes stay; the earlier
            // migration's Down drops their table.
            migrationBuilder.Sql("""
                UPDATE a
                SET a.[SchemeId] = s.[DefaultWorkflowId]
                FROM [StatusWorkflows].[WorkflowAssignments] a
                JOIN [StatusWorkflows].[WorkflowSchemes] s ON s.[Id] = a.[SchemeId];
                """);

            migrationBuilder.RenameColumn(
                name: "SchemeId",
                schema: "StatusWorkflows",
                table: "WorkflowAssignments",
                newName: "WorkflowId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkflowAssignments_SchemeId",
                schema: "StatusWorkflows",
                table: "WorkflowAssignments",
                newName: "IX_WorkflowAssignments_WorkflowId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowAssignments_StatusWorkflows_WorkflowId",
                schema: "StatusWorkflows",
                table: "WorkflowAssignments",
                column: "WorkflowId",
                principalSchema: "StatusWorkflows",
                principalTable: "StatusWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
