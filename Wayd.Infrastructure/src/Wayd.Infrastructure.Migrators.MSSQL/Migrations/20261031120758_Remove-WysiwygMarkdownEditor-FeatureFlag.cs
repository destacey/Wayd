using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class RemoveWysiwygMarkdownEditorFeatureFlag : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM [FeatureManagement].[FeatureFlags]
            WHERE [Name] = N'wysiwyg-markdown-editor';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM [FeatureManagement].[FeatureFlags]
                WHERE [Name] = N'wysiwyg-markdown-editor'
            )
            BEGIN
                INSERT INTO [FeatureManagement].[FeatureFlags]
                    ([Name], [DisplayName], [Description], [IsSystem], [IsEnabled], [IsArchived], [FiltersJson], [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy])
                VALUES
                    (N'wysiwyg-markdown-editor',
                     N'WYSIWYG Markdown Editor',
                     N'Replaces the markdown text editor on long-text fields with a document editor that formats as you type. Stored text stays markdown.',
                     CAST(1 AS bit),
                     CAST(0 AS bit),
                     CAST(0 AS bit),
                     NULL,
                     SYSUTCDATETIME(),
                     NULL,
                     SYSUTCDATETIME(),
                     NULL);
            END
            """);
    }
}
