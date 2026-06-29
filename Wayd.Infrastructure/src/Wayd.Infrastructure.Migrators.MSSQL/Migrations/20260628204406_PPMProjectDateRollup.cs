using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class PPMProjectDateRollup : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Backfill parent task date ranges bottom-up in ProjectTasks so every parent task
        // covers all of its direct and indirect dated children.
        migrationBuilder.Sql(
            """
            WHILE 1 = 1
            BEGIN
                UPDATE parent
                SET
                    parent.[PlannedStart] = CASE WHEN child.MinStart < parent.[PlannedStart] OR parent.[PlannedStart] IS NULL THEN child.MinStart ELSE parent.[PlannedStart] END,
                    parent.[PlannedEnd]   = CASE WHEN child.HasOpenEnded = 1 THEN NULL
                                                 WHEN child.MaxEnd > parent.[PlannedEnd] OR parent.[PlannedEnd] IS NULL THEN child.MaxEnd
                                                 ELSE parent.[PlannedEnd] END
                FROM [Ppm].[ProjectTasks] AS parent
                INNER JOIN (
                    SELECT
                        c.[ParentId] AS ParentId,
                        MIN(CASE WHEN c.[Type] = 'Milestone' THEN c.[PlannedDate] ELSE c.[PlannedStart] END) AS MinStart,
                        MAX(CASE WHEN c.[Type] = 'Milestone' THEN c.[PlannedDate] ELSE c.[PlannedEnd] END) AS MaxEnd,
                        MAX(CASE WHEN c.[Type] = 'Task' AND c.[PlannedStart] IS NOT NULL AND c.[PlannedEnd] IS NULL THEN 1 ELSE 0 END) AS HasOpenEnded
                    FROM [Ppm].[ProjectTasks] AS c
                    WHERE c.[ParentId] IS NOT NULL
                      AND (c.[PlannedStart] IS NOT NULL OR c.[PlannedDate] IS NOT NULL)
                    GROUP BY c.[ParentId]
                ) AS child ON child.ParentId = parent.[Id]
                WHERE parent.[Type] = 'Task'
                  AND (
                       parent.[PlannedStart] IS NULL
                       OR child.MinStart < parent.[PlannedStart]
                       OR (parent.[PlannedEnd] IS NOT NULL AND child.HasOpenEnded = 1)
                       OR (parent.[PlannedEnd] IS NOT NULL AND child.HasOpenEnded = 0 AND child.MaxEnd > parent.[PlannedEnd])
                  );

                IF @@ROWCOUNT = 0
                    BREAK;
            END
            """);

        // Update ProjectPhases based on root tasks (ParentId IS NULL)
        migrationBuilder.Sql(
            """
            UPDATE phase
            SET
                phase.[Start] = CASE WHEN root.MinStart < phase.[Start] OR phase.[Start] IS NULL THEN root.MinStart ELSE phase.[Start] END,
                phase.[End]   = CASE WHEN root.HasOpenEnded = 1 THEN NULL
                                     WHEN root.MaxEnd > phase.[End] OR phase.[End] IS NULL THEN root.MaxEnd
                                     ELSE phase.[End] END
            FROM [Ppm].[ProjectPhases] AS phase
            INNER JOIN (
                SELECT
                    t.[ProjectPhaseId] AS PhaseId,
                    MIN(CASE WHEN t.[Type] = 'Milestone' THEN t.[PlannedDate] ELSE t.[PlannedStart] END) AS MinStart,
                    MAX(CASE WHEN t.[Type] = 'Milestone' THEN t.[PlannedDate] ELSE t.[PlannedEnd] END) AS MaxEnd,
                    MAX(CASE WHEN t.[Type] = 'Task' AND t.[PlannedStart] IS NOT NULL AND t.[PlannedEnd] IS NULL THEN 1 ELSE 0 END) AS HasOpenEnded
                FROM [Ppm].[ProjectTasks] AS t
                WHERE t.[ParentId] IS NULL
                  AND (t.[PlannedStart] IS NOT NULL OR t.[PlannedDate] IS NOT NULL)
                GROUP BY t.[ProjectPhaseId]
            ) AS root ON root.PhaseId = phase.[Id]
            WHERE phase.[Start] IS NULL
               OR root.MinStart < phase.[Start]
               OR (phase.[End] IS NOT NULL AND root.HasOpenEnded = 1)
               OR (phase.[End] IS NOT NULL AND root.HasOpenEnded = 0 AND root.MaxEnd > phase.[End]);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
