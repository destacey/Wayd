using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class IncidentSeveritySets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Replacing 20261028190911 left some databases with these tables but a different history row.
        // Create only when missing so startup succeeds without dropping data.
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM [dbo].[__EFMigrationsHistory]
                WHERE [MigrationId] = N'20261028190911_IncidentSeveritySets')
            BEGIN
                DELETE FROM [dbo].[__EFMigrationsHistory]
                WHERE [MigrationId] = N'20261028190911_IncidentSeveritySets';
            END

            IF NOT EXISTS (
                SELECT 1
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'ProductManagement' AND t.name = N'IncidentSeveritySetAssignments')
            BEGIN
                CREATE TABLE [ProductManagement].[IncidentSeveritySetAssignments] (
                    [Id] uniqueidentifier NOT NULL,
                    [SeveritySetId] uniqueidentifier NOT NULL,
                    [SystemCreated] datetime2 NOT NULL,
                    [SystemCreatedBy] nvarchar(450) NULL,
                    [SystemLastModified] datetime2 NOT NULL,
                    [SystemLastModifiedBy] nvarchar(450) NULL,
                    CONSTRAINT [PK_IncidentSeveritySetAssignments] PRIMARY KEY ([Id])
                );
            END

            IF NOT EXISTS (
                SELECT 1
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'ProductManagement' AND t.name = N'IncidentSeveritySets')
            BEGIN
                CREATE TABLE [ProductManagement].[IncidentSeveritySets] (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(64) NOT NULL,
                    [Description] nvarchar(512) NULL,
                    [IsSystem] bit NOT NULL,
                    [IsActive] bit NOT NULL,
                    [SystemCreated] datetime2 NOT NULL,
                    [SystemCreatedBy] nvarchar(450) NULL,
                    [SystemLastModified] datetime2 NOT NULL,
                    [SystemLastModifiedBy] nvarchar(450) NULL,
                    CONSTRAINT [PK_IncidentSeveritySets] PRIMARY KEY ([Id])
                );
            END

            IF NOT EXISTS (
                SELECT 1
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'ProductManagement' AND t.name = N'IncidentSeverityLevels')
            BEGIN
                CREATE TABLE [ProductManagement].[IncidentSeverityLevels] (
                    [Id] uniqueidentifier NOT NULL,
                    [SetId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(64) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [SystemCreated] datetime2 NOT NULL,
                    [SystemCreatedBy] nvarchar(450) NULL,
                    [SystemLastModified] datetime2 NOT NULL,
                    [SystemLastModifiedBy] nvarchar(450) NULL,
                    CONSTRAINT [PK_IncidentSeverityLevels] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_IncidentSeverityLevels_IncidentSeveritySets_SetId] FOREIGN KEY ([SetId])
                        REFERENCES [ProductManagement].[IncidentSeveritySets] ([Id]) ON DELETE CASCADE
                );
            END

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes i
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'ProductManagement'
                  AND t.name = N'IncidentSeverityLevels'
                  AND i.name = N'IX_IncidentSeverityLevels_SetId_Name')
            BEGIN
                CREATE UNIQUE INDEX [IX_IncidentSeverityLevels_SetId_Name]
                    ON [ProductManagement].[IncidentSeverityLevels] ([SetId], [Name]);
            END

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes i
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'ProductManagement'
                  AND t.name = N'IncidentSeveritySets'
                  AND i.name = N'IX_IncidentSeveritySets_Name')
            BEGIN
                CREATE UNIQUE INDEX [IX_IncidentSeveritySets_Name]
                    ON [ProductManagement].[IncidentSeveritySets] ([Name]);
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "IncidentSeverityLevels",
            schema: "ProductManagement");

        migrationBuilder.DropTable(
            name: "IncidentSeveritySetAssignments",
            schema: "ProductManagement");

        migrationBuilder.DropTable(
            name: "IncidentSeveritySets",
            schema: "ProductManagement");
    }
}
