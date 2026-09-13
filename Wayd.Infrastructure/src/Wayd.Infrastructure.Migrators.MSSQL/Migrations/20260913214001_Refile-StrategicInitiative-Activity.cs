using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RefileStrategicInitiativeActivity : Migration
    {
        // StrategicInitiativeCreatedEvent and StrategicInitiativeDeletedEvent used to declare the portfolio
        // as their aggregate; from 1.1 they declare the initiative. The entries written before that are moved
        // to the initiative so its Activity section starts with its creation. Only the envelope moves — the
        // payload, timestamp and actor are the recorded fact and stay as written. The summary is rewritten to
        // what the factory produces for the new aggregate, which drops the " on Project Portfolio" suffix.
        //
        // Idempotent: a moved row no longer matches the AggregateType filter.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [App].[ActivityLogs]
                SET [AggregateType] = 'StrategicInitiative',
                    [AggregateId] = CONVERT(uniqueidentifier, JSON_VALUE([Payload], '$.strategicInitiativeId')),
                    [Summary] = CASE [EventType]
                        WHEN 'StrategicInitiativeCreatedEvent' THEN 'Strategic Initiative Created'
                        ELSE 'Strategic Initiative Deleted'
                    END
                WHERE [EventType] IN ('StrategicInitiativeCreatedEvent', 'StrategicInitiativeDeletedEvent')
                    AND [AggregateType] = 'ProjectPortfolio'
                    AND JSON_VALUE([Payload], '$.strategicInitiativeId') IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Returns every such entry to its portfolio, including ones written after this migration ran:
            // nothing distinguishes a moved row from a live one. Acceptable only because reverting this
            // migration means reverting to a build whose events named the portfolio.
            migrationBuilder.Sql(@"
                UPDATE [App].[ActivityLogs]
                SET [AggregateType] = 'ProjectPortfolio',
                    [AggregateId] = CONVERT(uniqueidentifier, JSON_VALUE([Payload], '$.portfolioId')),
                    [Summary] = CASE [EventType]
                        WHEN 'StrategicInitiativeCreatedEvent' THEN 'Strategic Initiative Created on Project Portfolio'
                        ELSE 'Strategic Initiative Deleted on Project Portfolio'
                    END
                WHERE [EventType] IN ('StrategicInitiativeCreatedEvent', 'StrategicInitiativeDeletedEvent')
                    AND [AggregateType] = 'StrategicInitiative'
                    AND JSON_VALUE([Payload], '$.portfolioId') IS NOT NULL;
            ");
        }
    }
}
