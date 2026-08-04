using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueUsersEmployeeIdIndex : Migration
    {
        // One user per employee is now a database invariant. Because the rule was previously enforced
        // only by the admin edit-user picker's client-side filtering, existing data may violate it, so
        // this migration runs in two steps:
        //   1. unlink duplicate users (keeping the most recently active one), writing a matching
        //      audit-trail row for each change under one correlation id,
        //   2. create the filtered unique index.

        private const string SystemUserId = "11111111-1111-1111-1111-111111111111"; // SystemIdentity.UserId
        private const string CorrelationId = "8f4d1c02-6b3e-4a95-9d71-0c58e2a7b413"; // lets the rollback remove exactly the audit rows it added, even if the backfill runs more than once.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_EmployeeId",
                schema: "Identity",
                table: "Users");

            // Resolve pre-existing duplicate links BEFORE the unique index is built: migrations run
            // automatically at host startup (app.Services.InitializeDatabases()), so a duplicate row left
            // in place would turn into a boot failure rather than a bad error message.
            //
            // The surviving row is the most recently active user for that employee (non-NULL LastActivityAt
            // first, most recent wins, then Id as a deterministic tiebreak): the account someone is
            // actually using keeps its PPM role assignments, roadmap visibility, and team membership. The
            // losing rows are unlinked, not deleted — the users keep working and an admin can re-link the
            // correct one from Settings → Users, whereas deleting an account would be unrecoverable here.
            //
            // Each unlink is audited so the change is attributable and reversible: without a trail an admin
            // sees a user who "lost" their employee for no recorded reason, and the old EmployeeId — the
            // only record of what the link used to be — is gone. Columns mirror what BaseDbContext writes at
            // runtime: TableName is the CLR entity name (not the SQL table), and PrimaryKey/OldValues/
            // NewValues are camelCase JSON.
            migrationBuilder.Sql($@"
                -- Capture the losing rows and their old link BEFORE mutating, so the update is the single
                -- source of truth and the audit rows record what was actually applied.
                SELECT
                    r.[Id],
                    r.[EmployeeId] AS OldEmployeeId
                INTO #DuplicateEmployeeLinks
                FROM (
                    SELECT
                        [Id],
                        [EmployeeId],
                        ROW_NUMBER() OVER (
                            PARTITION BY [EmployeeId]
                            ORDER BY
                                CASE WHEN [LastActivityAt] IS NULL THEN 1 ELSE 0 END,
                                [LastActivityAt] DESC,
                                [Id]
                        ) AS LinkRank
                    FROM [Identity].[Users]
                    WHERE [EmployeeId] IS NOT NULL
                ) r
                WHERE r.LinkRank > 1;

                -- Clear the duplicate links first.
                UPDATE u
                SET u.[EmployeeId] = NULL
                FROM [Identity].[Users] u
                INNER JOIN #DuplicateEmployeeLinks d ON d.[Id] = u.[Id];

                -- Then record an audit row for each user that was unlinked.
                INSERT INTO [Auditing].[AuditTrails]
                    ([Id], [UserId], [Type], [SchemaName], [TableName], [DateTime], [OldValues], [NewValues], [AffectedColumns], [PrimaryKey], [CorrelationId])
                SELECT
                    NEWID(),
                    '{SystemUserId}',
                    'Update',
                    'Identity',
                    'ApplicationUser',
                    SYSUTCDATETIME(),
                    -- PascalCase property names and lowercase GUIDs, matching the rows the app writes
                    -- (verified against existing Identity/PersonalAccessToken rows). SQL's CONVERT
                    -- yields uppercase GUIDs, hence the LOWER(). Getting this wrong would not just look
                    -- inconsistent: the rollback below reads OldValues with JSON_VALUE, which is
                    -- case-sensitive and would silently return NULL, clearing links instead of
                    -- restoring them.
                    '{{""EmployeeId"":""' + LOWER(CONVERT(varchar(36), d.OldEmployeeId)) + '""}}',
                    '{{""EmployeeId"":null}}',
                    '[""EmployeeId""]',
                    -- ApplicationUser.Id is an IdentityUser GUID string, so it needs no JSON escaping.
                    '{{""Id"":""' + CONVERT(varchar(450), d.[Id]) + '""}}',
                    '{CorrelationId}'
                FROM #DuplicateEmployeeLinks d;

                DROP TABLE #DuplicateEmployeeLinks;
            ");

            migrationBuilder.CreateIndex(
                name: "UX_Users_EmployeeId",
                schema: "Identity",
                table: "Users",
                column: "EmployeeId",
                unique: true,
                filter: "[EmployeeId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the unique index first — restoring the duplicate links below would violate it.
            migrationBuilder.DropIndex(
                name: "UX_Users_EmployeeId",
                schema: "Identity",
                table: "Users");

            // Restore each link this migration cleared, reading the old value back out of the audit row,
            // then remove those rows. Skipped entirely if any of the affected users has since had their
            // employee link changed by someone else (a later audit row for the same user/column, or a link
            // that is no longer NULL): replaying a stale value would silently overwrite the newer decision.
            migrationBuilder.Sql($@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM [Auditing].[AuditTrails] mine
                    WHERE mine.[CorrelationId] = '{CorrelationId}'
                      AND (
                            EXISTS (
                                SELECT 1
                                FROM [Auditing].[AuditTrails] later
                                WHERE later.[CorrelationId] <> '{CorrelationId}'
                                  AND later.[SchemaName] = 'Identity'
                                  AND later.[TableName] = 'ApplicationUser'
                                  AND later.[PrimaryKey] = mine.[PrimaryKey]
                                  AND later.[AffectedColumns] LIKE '%""EmployeeId""%'
                                  AND later.[DateTime] > mine.[DateTime]
                            )
                            OR EXISTS (
                                SELECT 1
                                FROM [Identity].[Users] u
                                WHERE '{{""Id"":""' + u.[Id] + '""}}' = mine.[PrimaryKey]
                                  AND u.[EmployeeId] IS NOT NULL
                            )
                      )
                )
                BEGIN
                    UPDATE u
                    SET u.[EmployeeId] = TRY_CONVERT(uniqueidentifier, JSON_VALUE(a.[OldValues], '$.EmployeeId'))
                    FROM [Identity].[Users] u
                    INNER JOIN [Auditing].[AuditTrails] a
                        ON a.[PrimaryKey] = '{{""Id"":""' + u.[Id] + '""}}'
                    WHERE a.[CorrelationId] = '{CorrelationId}'
                      AND a.[SchemaName] = 'Identity'
                      AND a.[TableName] = 'ApplicationUser'
                      AND a.[Type] = 'Update'
                      AND a.[AffectedColumns] = '[""EmployeeId""]';

                    DELETE FROM [Auditing].[AuditTrails]
                    WHERE [CorrelationId] = '{CorrelationId}'
                      AND [SchemaName] = 'Identity'
                      AND [TableName] = 'ApplicationUser'
                      AND [Type] = 'Update'
                      AND [AffectedColumns] = '[""EmployeeId""]';
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                schema: "Identity",
                table: "Users",
                column: "EmployeeId");
        }
    }
}
