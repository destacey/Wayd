using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class ImproveWorkItemSearchPerformance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkItems_ParentId' AND object_id = OBJECT_ID('Work.WorkItems'))
                DROP INDEX [IX_WorkItems_ParentId] ON [Work].[WorkItems];");

        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'KeyNumber' AND object_id = OBJECT_ID('Work.WorkItems'))
                ALTER TABLE [Work].[WorkItems]
                    ADD [KeyNumber] AS CAST(SUBSTRING([Key], CHARINDEX('-', [Key]) + 1, LEN([Key])) AS int) PERSISTED;");

        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'KeyPrefix' AND object_id = OBJECT_ID('Work.WorkItems'))
                ALTER TABLE [Work].[WorkItems]
                    ADD [KeyPrefix] AS LEFT([Key], CHARINDEX('-', [Key]) - 1) PERSISTED;");

        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkItems_KeyPrefix_KeyNumber' AND object_id = OBJECT_ID('Work.WorkItems'))
                CREATE INDEX [IX_WorkItems_KeyPrefix_KeyNumber] ON [Work].[WorkItems] ([KeyPrefix], [KeyNumber]) INCLUDE ([Id]);");

        migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkItems_ParentId_Key' AND object_id = OBJECT_ID('Work.WorkItems'))
                CREATE INDEX [IX_WorkItems_ParentId_Key] ON [Work].[WorkItems] ([ParentId]) INCLUDE ([Key]) WHERE [ParentId] IS NOT NULL;");

        // Full-text search requires the FTS feature to be installed (not available on all instances).
        migrationBuilder.Sql(@"
            IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
            BEGIN
                EXEC('CREATE FULLTEXT CATALOG [FtCatalog_WorkItems] AS DEFAULT')
            END", suppressTransaction: true);

        migrationBuilder.Sql(@"
            IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
            BEGIN
                EXEC('
                    CREATE FULLTEXT INDEX ON [work].[WorkItems]
                    (
                        [Title] LANGUAGE 1033,
                        [Key]   LANGUAGE 1033
                    )
                    KEY INDEX [UIX_WorkItems_Key]
                    ON [FtCatalog_WorkItems]
                    WITH CHANGE_TRACKING AUTO
                ')
            END", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
            BEGIN
                EXEC('DROP FULLTEXT INDEX ON [work].[WorkItems]')
                EXEC('DROP FULLTEXT CATALOG [FtCatalog_WorkItems]')
            END", suppressTransaction: true);

        migrationBuilder.DropIndex(
            name: "IX_WorkItems_ParentId_Key",
            schema: "Work",
            table: "WorkItems");

        migrationBuilder.DropIndex(
            name: "IX_WorkItems_KeyPrefix_KeyNumber",
            schema: "Work",
            table: "WorkItems");

        migrationBuilder.DropColumn(
            name: "KeyNumber",
            schema: "Work",
            table: "WorkItems");

        migrationBuilder.DropColumn(
            name: "KeyPrefix",
            schema: "Work",
            table: "WorkItems");

        migrationBuilder.CreateIndex(
            name: "IX_WorkItems_ParentId",
            schema: "Work",
            table: "WorkItems",
            column: "ParentId");
    }
}
