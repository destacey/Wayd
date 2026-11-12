using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDefaultIncidentSeverityColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // System scales were seeded before Color existed, so the column arrived blank.
            // These are IncidentSeverityLevel.DefaultColorForSortOrder. A stored color stays,
            // and a custom scale is not a system scale.
            migrationBuilder.Sql(
                """
                UPDATE l
                SET [Color] = CASE l.[SortOrder]
                    WHEN 0 THEN N'#E07A4C'
                    WHEN 1 THEN N'#D4A017'
                    WHEN 2 THEN N'#7FA3BF'
                    ELSE N'#8E9E98'
                END
                FROM [ProductManagement].[IncidentSeverityLevels] AS l
                INNER JOIN [ProductManagement].[IncidentSeveritySets] AS s ON s.[Id] = l.[SetId]
                WHERE s.[IsSystem] = 1
                  AND (l.[Color] IS NULL OR LTRIM(RTRIM(l.[Color])) = N'');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A blank color is not valid, and a rollback cannot tell a row this filled
            // from one that already held the same default.
        }
    }
}
