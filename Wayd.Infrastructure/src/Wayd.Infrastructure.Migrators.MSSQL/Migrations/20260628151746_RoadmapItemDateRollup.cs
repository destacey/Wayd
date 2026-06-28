using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class RoadmapItemDateRollup : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop the index that duplicated the RoadmapManagers primary key (RoadmapId, ManagerId).
        migrationBuilder.DropIndex(
            name: "IX_RoadmapManagers_RoadmapId_ManagerId",
            schema: "Planning",
            table: "RoadmapManagers");

        // Backfill parent date ranges so every Activity contains all of its descendants. This
        // brings existing roadmaps in line with the date-rollup rule introduced in the domain,
        // where a parent Activity always grows to cover its children (grow-only; never shrinks).
        //
        // Date columns: Activity/Timebox use [Start, End]; Milestone uses [Start] as its date
        // (its effective range is [Start, Start]). Only Activities (Type = 1) can be parents.
        //
        // Hierarchies can be multiple levels deep, so we grow each parent from the span of its
        // direct children and repeat until a pass makes no changes (a fixpoint), which lets the
        // growth bubble all the way to the root.
        migrationBuilder.Sql(
            """
            WHILE 1 = 1
            BEGIN
                UPDATE parent
                SET
                    parent.[Start] = CASE WHEN child.MinStart < parent.[Start] THEN child.MinStart ELSE parent.[Start] END,
                    parent.[End]   = CASE WHEN child.MaxEnd   > parent.[End]   THEN child.MaxEnd   ELSE parent.[End]   END
                FROM [Planning].[RoadmapItems] AS parent
                INNER JOIN (
                    SELECT
                        c.[ParentId] AS ParentId,
                        MIN(c.[Start]) AS MinStart,
                        MAX(CASE WHEN c.[Type] = 2 THEN c.[Start] ELSE c.[End] END) AS MaxEnd
                    FROM [Planning].[RoadmapItems] AS c
                    WHERE c.[ParentId] IS NOT NULL
                    GROUP BY c.[ParentId]
                ) AS child ON child.ParentId = parent.[Id]
                WHERE parent.[Type] = 1
                  AND (child.MinStart < parent.[Start] OR child.MaxEnd > parent.[End]);

                IF @@ROWCOUNT = 0
                    BREAK;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Recreate the dropped index. The date backfill is a one-way data change with no
        // retained original values, so there is nothing to revert for it.
        migrationBuilder.CreateIndex(
            name: "IX_RoadmapManagers_RoadmapId_ManagerId",
            schema: "Planning",
            table: "RoadmapManagers",
            columns: ["RoadmapId", "ManagerId"]);
    }
}
