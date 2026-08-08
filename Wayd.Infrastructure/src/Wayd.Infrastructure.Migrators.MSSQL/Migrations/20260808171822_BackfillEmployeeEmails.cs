using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class BackfillEmployeeEmails : Migration
{
    // Seeds Organization.EmployeeEmails with the canonical address of every employee that has no row
    // yet.
    //
    // The people sync writes this collection, but only for employees present in its payload. Anyone the
    // active connector does not report — someone created under a previous connector, a worker outside
    // the current sync scope, a leaver — is deactivated and never touched again, so those rows were
    // left with no addresses at all. That makes them unresolvable in the Azure DevOps work-item
    // matching this table exists to serve, which is exactly the historical attribution the collection
    // is meant to preserve.

    private const string SystemUserId = "11111111-1111-1111-1111-111111111111"; // SystemIdentity.UserId
    private const string CorrelationId = "3d9a5f71-84c2-4e60-b1d7-6f2a0c94e5b8"; // lets Down remove exactly the rows this migration added.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The insert and its audit rows are driven from one @Backfilled table variable, so the trail
        // cannot describe rows that were not written (or miss ones that were).
        //
        // Columns mirror what the application writes at runtime: TableName is the CLR entity name (not
        // the SQL table) and PrimaryKey/NewValues are PascalCase JSON with lowercase GUIDs. SQL's
        // CONVERT yields uppercase, hence LOWER() — the casing is load-bearing, because Down matches
        // PrimaryKey by string equality and would silently delete nothing if it disagreed. Email is
        // nested under a "value" key because it is an EmailAddress value object, matching how EF
        // serializes it.
        migrationBuilder.Sql($@"
                DECLARE @Now datetime2(7) = SYSUTCDATETIME();

                DECLARE @Backfilled TABLE
                (
                    Id uniqueidentifier NOT NULL,
                    EmployeeId uniqueidentifier NOT NULL,
                    Email nvarchar(256) NOT NULL
                );

                -- Soft-deleted employees are excluded: the global query filter hides them everywhere else,
                -- so rows here would only be addresses nothing can resolve against. Inactive employees ARE
                -- included — a former employee still authored and was assigned work items, and losing that
                -- attribution is the problem this table solves.
                INSERT INTO [Organization].[EmployeeEmails]
                    ([Id], [Email], [EmployeeId], [IsPrimary], [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy])
                OUTPUT inserted.[Id], inserted.[EmployeeId], inserted.[Email] INTO @Backfilled
                SELECT
                    NEWID(),
                    e.[Email],
                    e.[Id],
                    1,
                    @Now,
                    '{SystemUserId}',
                    @Now,
                    '{SystemUserId}'
                FROM [Organization].[Employees] e
                WHERE e.[IsDeleted] = 0
                  -- Guards the unique index on EmployeeEmails.Email, which spans the whole table: an
                  -- address already held by anyone — including a different employee that recycled it —
                  -- must not be inserted a second time. Also what makes re-running this a no-op.
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [Organization].[EmployeeEmails] ee
                      WHERE ee.[Email] = e.[Email]
                  );

                INSERT INTO [Auditing].[AuditTrails]
                    ([Id], [UserId], [Type], [SchemaName], [TableName], [DateTime], [OldValues], [NewValues], [AffectedColumns], [PrimaryKey], [CorrelationId])
                SELECT
                    NEWID(),
                    '{SystemUserId}',
                    'Create',
                    'Organization',
                    'EmployeeEmail',
                    @Now,
                    NULL,
                    '{{""Email"":{{""value"":""' + STRING_ESCAPE(b.[Email], 'json') + '""}}' +
                        ',""EmployeeId"":""' + LOWER(CONVERT(varchar(36), b.[EmployeeId])) + '""' +
                        ',""IsPrimary"":true' +
                        ',""SystemCreated"":""' + CONVERT(varchar(33), @Now, 127) + '""' +
                        ',""SystemCreatedBy"":""{SystemUserId}""' +
                        ',""SystemLastModified"":""' + CONVERT(varchar(33), @Now, 127) + '""' +
                        ',""SystemLastModifiedBy"":""{SystemUserId}""}}',
                    -- NULL, matching what the app writes: the auditing interceptor only populates
                    -- AffectedColumns for Modified entries, never for Added ones.
                    NULL,
                    '{{""Id"":""' + LOWER(CONVERT(varchar(36), b.[Id])) + '""}}',
                    '{CorrelationId}'
                FROM @Backfilled b;
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Removes exactly the rows this migration created, identified through its own audit trail —
        // rows a people sync has written since are indistinguishable by shape alone, so the correlation
        // id is the only safe handle.
        //
        // Skipped entirely if any backfilled address has since been touched by something else (a later
        // audit row for the same row, or an employee whose collection has grown beyond the one address
        // this migration gave them): removing it then would discard a decision made after this ran.
        migrationBuilder.Sql($@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM [Auditing].[AuditTrails] mine
                    WHERE mine.[CorrelationId] = '{CorrelationId}'
                      AND EXISTS (
                            SELECT 1
                            FROM [Auditing].[AuditTrails] later
                            WHERE later.[CorrelationId] <> '{CorrelationId}'
                              AND later.[SchemaName] = 'Organization'
                              AND later.[TableName] = 'EmployeeEmail'
                              AND later.[PrimaryKey] = mine.[PrimaryKey]
                              AND later.[DateTime] > mine.[DateTime]
                      )
                )
                BEGIN
                    DELETE ee
                    FROM [Organization].[EmployeeEmails] ee
                    INNER JOIN [Auditing].[AuditTrails] a
                        ON a.[PrimaryKey] = '{{""Id"":""' + LOWER(CONVERT(varchar(36), ee.[Id])) + '""}}'
                    WHERE a.[CorrelationId] = '{CorrelationId}'
                      AND a.[SchemaName] = 'Organization'
                      AND a.[TableName] = 'EmployeeEmail'
                      AND a.[Type] = 'Create';

                    DELETE FROM [Auditing].[AuditTrails]
                    WHERE [CorrelationId] = '{CorrelationId}'
                      AND [SchemaName] = 'Organization'
                      AND [TableName] = 'EmployeeEmail'
                      AND [Type] = 'Create';
                END
            ");
    }
}
