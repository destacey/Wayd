using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class MoveWorkItemsOntoStatusWorkflows : Migration
    {
        /// <summary>Drops the work item title search index, where full-text search is installed and it exists.</summary>
        private const string DropWorkItemFullTextIndex = """
            IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Work.WorkItems'))
                EXEC('DROP FULLTEXT INDEX ON [Work].[WorkItems]')
            """;

        /// <summary>Recreates the work item title search index as NarrowWorkItemFullTextIndexToTitle left it.</summary>
        private const string CreateWorkItemFullTextIndex = """
            IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'FtCatalog_WorkItems')
                    EXEC('CREATE FULLTEXT CATALOG [FtCatalog_WorkItems]')

                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Work.WorkItems'))
                    EXEC('
                        CREATE FULLTEXT INDEX ON [Work].[WorkItems]
                        (
                            [Title] LANGUAGE 1033
                        )
                        KEY INDEX [IX_WorkItems_Key]
                        ON [FtCatalog_WorkItems]
                        WITH CHANGE_TRACKING AUTO
                    ')
            END
            """;

        /// <inheritdoc />
        /// <remarks>
        /// Work items keep their status's name and category, so every page reads the same before the next sync.
        /// Their engine status is left empty (Guid.Empty, "no status yet") and history periods lose their status
        /// id: managed workspaces are flagged for a full sync, which resolves every item through the workspace's
        /// scheme and rebuilds history from the stored revisions.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IX_WorkItems_Key is the full-text index's key, so SQL Server refuses to drop it while that index
            // exists. Full-text statements cannot run in a transaction; both are guarded, so a migration that fails
            // between them can be run again.
            migrationBuilder.Sql(DropWorkItemFullTextIndex, suppressTransaction: true);

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkStatuses_StatusId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemStateHistory_WorkStatuses_StatusId",
                schema: "Work",
                table: "WorkItemStateHistory");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemStateHistory_StatusId",
                schema: "Work",
                table: "WorkItemStateHistory");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_StatusId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ExternalId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Id",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_IterationId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Key",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_StatusCategory",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_WorkspaceId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Key_Title",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.AddColumn<string>(
                name: "StatusName",
                schema: "Work",
                table: "WorkItems",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "StatusWorkflowId",
                schema: "Work",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "StatusAliasValue",
                schema: "Work",
                table: "WorkItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // WorkItem raises no domain events, so copying onto it here bypasses nothing.
            migrationBuilder.Sql("""
                UPDATE wi
                SET wi.StatusName = ws.Name
                FROM [Work].[WorkItems] wi
                JOIN [Work].[WorkStatuses] ws ON ws.Id = wi.StatusId;
                """);

            migrationBuilder.DropColumn(
                name: "StatusId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.AddColumn<Guid>(
                name: "StatusId",
                schema: "Work",
                table: "WorkItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.DropColumn(
                name: "StatusId",
                schema: "Work",
                table: "WorkItemStateHistory");

            migrationBuilder.AddColumn<Guid>(
                name: "StatusId",
                schema: "Work",
                table: "WorkItemStateHistory",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordTypeId",
                schema: "Work",
                table: "WorkTypes",
                type: "uniqueidentifier",
                nullable: true);

            // The sync keys each work type's record type by Work's own name for it (ManagedWorkflowKeys.RecordType).
            migrationBuilder.Sql("""
                UPDATE wt
                SET wt.RecordTypeId = rt.Id
                FROM [Work].[WorkTypes] wt
                JOIN [StatusWorkflows].[RecordTypes] rt ON rt.OwnerType = 'work.item' AND rt.ExternalKey = wt.Name;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsFullSync",
                schema: "Work",
                table: "Workspaces",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE [Work].[Workspaces]
                SET NeedsFullSync = 1
                WHERE Ownership = 'Managed';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ExternalId",
                schema: "Work",
                table: "WorkItems",
                column: "ExternalId")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Title", "WorkspaceId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Id",
                schema: "Work",
                table: "WorkItems",
                column: "Id")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "WorkspaceId", "ExternalId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_IterationId",
                schema: "Work",
                table: "WorkItems",
                column: "IterationId")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Title", "WorkspaceId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Key",
                schema: "Work",
                table: "WorkItems",
                column: "Key",
                unique: true)
                .Annotation("SqlServer:Include", new[] { "Id", "Title", "WorkspaceId", "ExternalId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_StatusCategory",
                schema: "Work",
                table: "WorkItems",
                column: "StatusCategory")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Title", "WorkspaceId", "AssignedToId", "TypeId", "StatusId", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_WorkspaceId",
                schema: "Work",
                table: "WorkItems",
                column: "WorkspaceId")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Title", "ExternalId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Key_Title",
                schema: "Work",
                table: "WorkItems",
                columns: new[] { "Key", "Title" })
                .Annotation("SqlServer:Include", new[] { "Id", "WorkspaceId", "ExternalId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_StatusWorkflowId",
                schema: "Work",
                table: "WorkItems",
                column: "StatusWorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_StatusId",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "StatusId",
                filter: "[StatusId] IS NOT NULL");

            migrationBuilder.Sql(CreateWorkItemFullTextIndex, suppressTransaction: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Restores each item's Work status by name, which Work's statuses hold uniquely, adding any name Work does not
        /// have; a history period regains one the same way, matched on the name its source recorded.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // IX_WorkItems_Key is the full-text index's key, so SQL Server refuses to drop it while that index
            // exists. Full-text statements cannot run in a transaction; both are guarded, so a rollback that fails
            // between them can be run again.
            migrationBuilder.Sql(DropWorkItemFullTextIndex, suppressTransaction: true);

            migrationBuilder.DropIndex(
                name: "IX_WorkItemStateHistory_StatusId",
                schema: "Work",
                table: "WorkItemStateHistory");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_StatusWorkflowId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ExternalId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Id",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_IterationId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Key",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_StatusCategory",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_WorkspaceId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Key_Title",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RecordTypeId",
                schema: "Work",
                table: "WorkTypes");

            migrationBuilder.DropColumn(
                name: "NeedsFullSync",
                schema: "Work",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "StatusId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                schema: "Work",
                table: "WorkItems",
                type: "int",
                nullable: true);

            // A status the engine named that Work never had (a state first synced after the move) needs a row of
            // its own, or its items would have no status to return to.
            migrationBuilder.Sql("""
                INSERT INTO [Work].[WorkStatuses] ([Name], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                SELECT DISTINCT wi.StatusName, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [Work].[WorkItems] wi
                WHERE wi.StatusName <> ''
                  AND NOT EXISTS (SELECT 1 FROM [Work].[WorkStatuses] ws WHERE ws.Name = wi.StatusName);
                """);

            migrationBuilder.Sql("""
                UPDATE wi
                SET wi.StatusId = ws.Id
                FROM [Work].[WorkItems] wi
                JOIN [Work].[WorkStatuses] ws ON ws.Name = wi.StatusName;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "StatusId",
                schema: "Work",
                table: "WorkItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "StatusAliasValue",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "StatusName",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "StatusWorkflowId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "StatusId",
                schema: "Work",
                table: "WorkItemStateHistory");

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                schema: "Work",
                table: "WorkItemStateHistory",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE h
                SET h.StatusId = ws.Id
                FROM [Work].[WorkItemStateHistory] h
                JOIN [Work].[WorkStatuses] ws ON ws.Name = h.StatusName;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ExternalId",
                schema: "Work",
                table: "WorkItems",
                column: "ExternalId")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Title", "WorkspaceId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Id",
                schema: "Work",
                table: "WorkItems",
                column: "Id")
                .Annotation("SqlServer:Include", new[] { "Key", "Title", "WorkspaceId", "ExternalId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_IterationId",
                schema: "Work",
                table: "WorkItems",
                column: "IterationId")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Title", "WorkspaceId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Key",
                schema: "Work",
                table: "WorkItems",
                column: "Key",
                unique: true)
                .Annotation("SqlServer:Include", new[] { "Id", "Title", "WorkspaceId", "ExternalId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_StatusCategory",
                schema: "Work",
                table: "WorkItems",
                column: "StatusCategory")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Title", "WorkspaceId", "AssignedToId", "TypeId", "StatusId", "ActivatedTimestamp", "DoneTimestamp", "ProjectId", "ParentProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_WorkspaceId",
                schema: "Work",
                table: "WorkItems",
                column: "WorkspaceId")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Title", "ExternalId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Key_Title",
                schema: "Work",
                table: "WorkItems",
                columns: new[] { "Key", "Title" })
                .Annotation("SqlServer:Include", new[] { "Id", "WorkspaceId", "ExternalId", "AssignedToId", "TypeId", "StatusId", "StatusCategory", "ActivatedTimestamp", "DoneTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStateHistory_StatusId",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_StatusId",
                schema: "Work",
                table: "WorkItems",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkStatuses_StatusId",
                schema: "Work",
                table: "WorkItems",
                column: "StatusId",
                principalSchema: "Work",
                principalTable: "WorkStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItemStateHistory_WorkStatuses_StatusId",
                schema: "Work",
                table: "WorkItemStateHistory",
                column: "StatusId",
                principalSchema: "Work",
                principalTable: "WorkStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(CreateWorkItemFullTextIndex, suppressTransaction: true);
        }
    }
}
