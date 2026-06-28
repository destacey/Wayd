using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class NarrowWorkItemFullTextIndexToTitle : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The full-text index originally covered [Title] and [Key]. Key is a value-converted
        // value object that the FTS query translator cannot reference, so it is now searched
        // via LIKE and dropped from the index. Recreate the index over [Title] only.
        migrationBuilder.Sql(@"
                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Work.WorkItems'))
                        EXEC('DROP FULLTEXT INDEX ON [Work].[WorkItems]')

                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'FtCatalog_WorkItems')
                        EXEC('CREATE FULLTEXT CATALOG [FtCatalog_WorkItems]')

                    EXEC('
                        CREATE FULLTEXT INDEX ON [Work].[WorkItems]
                        (
                            [Title] LANGUAGE 1033
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
        // Restore the original index covering both [Title] and [Key].
        migrationBuilder.Sql(@"
                IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('Work.WorkItems'))
                        EXEC('DROP FULLTEXT INDEX ON [Work].[WorkItems]')

                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'FtCatalog_WorkItems')
                        EXEC('CREATE FULLTEXT CATALOG [FtCatalog_WorkItems]')

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
}
