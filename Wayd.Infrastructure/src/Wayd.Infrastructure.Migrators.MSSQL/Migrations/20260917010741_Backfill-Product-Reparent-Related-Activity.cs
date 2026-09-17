using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillProductReparentRelatedActivity : Migration
    {
        // ProductReparentedEventV2 names both parents as related aggregates, so a move shows in each parent's
        // Activity. Entries written before that are given the same related rows from the parent ids their
        // payloads already carry. The superseded ProductReparentedEvent recorded the same two ids, so its
        // entries are included. Only the related rows are added; the entries themselves are untouched.
        //
        // Idempotent: a parent already related to an entry is skipped.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO [App].[ActivityLogRelatedAggregates] ([ActivityLogId], [AggregateType], [AggregateId])
                SELECT DISTINCT a.[Id], 'Product', parent.[Id]
                FROM [App].[ActivityLogs] a
                CROSS APPLY (VALUES
                    (TRY_CONVERT(uniqueidentifier, JSON_VALUE(a.[Payload], '$.fromParentId'))),
                    (TRY_CONVERT(uniqueidentifier, JSON_VALUE(a.[Payload], '$.toParentId')))
                ) parent([Id])
                WHERE a.[EventType] IN ('ProductReparentedEvent', 'ProductReparentedEventV2')
                    AND a.[AggregateType] = 'Product'
                    AND parent.[Id] IS NOT NULL
                    AND parent.[Id] <> a.[AggregateId]
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [App].[ActivityLogRelatedAggregates] r
                        WHERE r.[ActivityLogId] = a.[Id]
                            AND r.[AggregateType] = 'Product'
                            AND r.[AggregateId] = parent.[Id]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Also removes the rows live moves wrote after this migration ran: nothing distinguishes a backfilled
            // row from one written by the event. Acceptable only because reverting this migration means reverting
            // to a build whose event named no related aggregates.
            migrationBuilder.Sql(@"
                DELETE r
                FROM [App].[ActivityLogRelatedAggregates] r
                INNER JOIN [App].[ActivityLogs] a ON a.[Id] = r.[ActivityLogId]
                WHERE a.[EventType] IN ('ProductReparentedEvent', 'ProductReparentedEventV2')
                    AND r.[AggregateType] = 'Product';
            ");
        }
    }
}
