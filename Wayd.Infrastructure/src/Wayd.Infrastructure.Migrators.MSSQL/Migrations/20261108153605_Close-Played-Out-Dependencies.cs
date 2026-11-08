using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class ClosePlayedOutDependencies : Migration
    {
        // Gives the planning health Closed to every stored dependency WorkItemDependency.CalculateHealth now closes:
        // a done predecessor, or a removed one whose successor is done or removed too. Health is otherwise only
        // recalculated when a sync processes the dependency, which never happens on a connection that no longer
        // syncs. Deleted links (RemovedOn set) keep Unknown. Running it again changes nothing.
        //
        // Values are the stored ints, frozen here: DependencyPlanningHealth Healthy 1, Unhealthy 3, Closed 5;
        // DependencyState Done 3, Removed 4; StatusCategory Done 2, Removed 3.

        /// <summary>Closes what the current rule closes; public so a test can run it against a real database.</summary>
        public const string UpSql = """
            UPDATE [Work].[WorkItemLinks]
            SET [Health] = 5
            WHERE [LinkType] = 'Dependency'
              AND [RemovedOn] IS NULL
              AND [Health] <> 5
              AND ([State] = 3
                OR ([State] = 4 AND [TargetStatusCategory] IN (2, 3)));
            """;

        /// <summary>
        /// Restores the health the previous rule gave: Healthy for a done predecessor, Unhealthy for a removed one.
        /// </summary>
        public const string DownSql = """
            UPDATE [Work].[WorkItemLinks]
            SET [Health] = CASE [State] WHEN 3 THEN 1 ELSE 3 END
            WHERE [LinkType] = 'Dependency'
              AND [RemovedOn] IS NULL
              AND [Health] = 5
              AND [State] IN (3, 4);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DownSql);
    }
}
