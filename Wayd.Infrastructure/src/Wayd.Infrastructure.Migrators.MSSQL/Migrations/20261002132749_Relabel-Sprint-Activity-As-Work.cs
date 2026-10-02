using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class RelabelSprintActivityAsWork : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A sprint's entries take the domain area of the module that owns it, now Work. Those written before
        // the move say Planning; relabelling them keeps one sprint's history under one area. Only the label
        // changes: the event type, payload and everything else an entry records stay as written.
        migrationBuilder.Sql(@"
            UPDATE [App].[ActivityLogs]
            SET [DomainArea] = 'Work'
            WHERE [AggregateType] = 'Iteration'
              AND [DomainArea] = 'Planning';");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE [App].[ActivityLogs]
            SET [DomainArea] = 'Planning'
            WHERE [AggregateType] = 'Iteration'
              AND [DomainArea] = 'Work';");
    }
}
