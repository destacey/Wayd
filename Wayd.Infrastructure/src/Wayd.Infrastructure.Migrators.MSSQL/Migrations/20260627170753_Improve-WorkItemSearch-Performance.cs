using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class ImproveWorkItemSearchPerformance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_WorkItems_ParentId",
            schema: "Work",
            table: "WorkItems");

        migrationBuilder.AddColumn<int>(
            name: "KeyNumber",
            schema: "Work",
            table: "WorkItems",
            type: "int",
            nullable: false,
            computedColumnSql: "CAST(SUBSTRING([Key], CHARINDEX('-', [Key]) + 1, LEN([Key])) AS int)",
            stored: true);

        migrationBuilder.AddColumn<string>(
            name: "KeyPrefix",
            schema: "Work",
            table: "WorkItems",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true,
            computedColumnSql: "LEFT([Key], CHARINDEX('-', [Key]) - 1)",
            stored: true);

        migrationBuilder.CreateIndex(
            name: "IX_WorkItems_KeyPrefix_KeyNumber",
            schema: "Work",
            table: "WorkItems",
            columns: new[] { "KeyPrefix", "KeyNumber" })
            .Annotation("SqlServer:Include", new[] { "Id" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkItems_ParentId_Key",
            schema: "Work",
            table: "WorkItems",
            column: "ParentId",
            filter: "[ParentId] IS NOT NULL")
            .Annotation("SqlServer:Include", new[] { "Key" });

        // Full-text search requires the FTS feature to be installed (not available on all instances).
        migrationBuilder.Sql(@"
            IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
              AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'FtCatalog_WorkItems')
            BEGIN
                EXEC('CREATE FULLTEXT CATALOG [FtCatalog_WorkItems]')
            END", suppressTransaction: true);

        migrationBuilder.Sql(@"
            IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
              AND NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Work.WorkItems'))
            BEGIN
                EXEC('
                    CREATE FULLTEXT INDEX ON [Work].[WorkItems]
                    (
                        [Title] LANGUAGE 1033,
                        [Key]   LANGUAGE 1033
                    )
                    KEY INDEX [IX_WorkItems_Key]
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
                IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Work.WorkItems'))
                    EXEC('DROP FULLTEXT INDEX ON [Work].[WorkItems]')
                IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'FtCatalog_WorkItems')
                    EXEC('DROP FULLTEXT CATALOG [FtCatalog_WorkItems]')
            END", suppressTransaction: true);

        migrationBuilder.DropIndex(
            name: "IX_WorkItems_KeyPrefix_KeyNumber",
            schema: "Work",
            table: "WorkItems");

        migrationBuilder.DropIndex(
            name: "IX_WorkItems_ParentId_Key",
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
