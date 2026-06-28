using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class RemoveNewTimelineUiFeatureFlag : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM [FeatureManagement].[FeatureFlags]
            WHERE [Name] = N'new-timeline-ui';
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
                WHERE [Name] = N'new-timeline-ui'
            )
            BEGIN
                INSERT INTO [FeatureManagement].[FeatureFlags]
                    ([Name], [DisplayName], [Description], [IsSystem], [IsEnabled], [IsArchived], [FiltersJson], [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy])
                VALUES
                    (N'new-timeline-ui',
                     N'Use New Timeline UI',
                     N'Renders roadmaps (and later objectives/PPM) with the new in-house timeline component instead of the legacy vis-timeline.',
                     CAST(1 AS bit),
                     CAST(1 AS bit),
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
