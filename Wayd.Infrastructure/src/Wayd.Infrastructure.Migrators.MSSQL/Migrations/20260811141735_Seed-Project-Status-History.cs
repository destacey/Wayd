using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class SeedProjectStatusHistory : Migration
{
    // Reconstructs each project's status history from the audit trail, so projects that existed before
    // Ppm.ProjectStatusHistory have a timeline rather than starting blank.
    //
    // Three things about the trail shape drive this SQL, all verified against real data rather than
    // inferred:
    //
    //   TableName is the CLR entity name, 'Project', not the mapped table 'Projects'.
    //   PrimaryKey is {"Id":"<guid>"} — a PascalCase key.
    //   Within one document the KEYS are PascalCase but the enum VALUES are camelCase:
    //       {"Status":"proposed","SystemLastModified":"2026-06-27T14:20:09Z"}
    //   OldValues/NewValues are Dictionary<string, object?> keyed on EF property names.
    //   PropertyNamingPolicy = CamelCase renames reflected POCO properties but NOT dictionary keys
    //   (that needs DictionaryKeyPolicy, which is not set), while JsonStringEnumConverter does
    //   camelCase the values. Ppm.Projects.Status is varchar holding PascalCase, so the value has to
    //   be mapped back on the way in.
    //
    // Rows are marked Reconstructed (or Synthesized) rather than Recorded so a seeded row never claims
    // the fidelity of one written as the change happened. That mark is also what Down keys on.
    //
    // No audit trail rows are written for the inserts. ProjectStatusHistory derives from BaseEntity,
    // not BaseAuditableEntity, so it is not ISystemAuditable and the interceptor never writes a trail
    // for it at runtime; seeding one here would invent a shape the application never produces. The
    // table is itself the history record, and its rows are immutable — there is nothing to audit.

    private const string SystemUserId = "11111111-1111-1111-1111-111111111111"; // SystemIdentity.UserId

    // The reconstruction reads every audit trail row: TableName is not indexed, so the filter to
    // 'Project' cannot seek. Measured at roughly twelve seconds against a trail of ~550k rows — inside
    // the migration timeout DatabaseInitializer applies, but outside the 30-second ADO.NET default the
    // context would otherwise use, and the trail only grows.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($@"
                -- Malformed rows exist in the trail — bare GUIDs where PrimaryKey should be a JSON
                -- document, and null bodies on Create rows — and an unguarded JSON_VALUE against one
                -- aborts the whole batch rather than skipping the row.
                --
                -- Every JSON read is therefore wrapped in CASE WHEN ISJSON(...) = 1, not merely
                -- filtered by an ISJSON predicate in the WHERE. SQL Server does not guarantee predicate
                -- evaluation order, so a WHERE-clause guard can be evaluated *after* the JSON_VALUE it
                -- is meant to protect; CASE is the one construct with defined order. This is not
                -- theoretical — the WHERE-guarded form of this query fails on this data.
                WITH Extracted AS
                (
                    SELECT
                        CASE WHEN ISJSON(t.[PrimaryKey]) = 1
                             THEN JSON_VALUE(t.[PrimaryKey], '$.Id') END AS ProjectIdRaw,
                        t.[UserId] AS ChangedByUserId,
                        t.[DateTime] AS ChangedOn,
                        t.[Type] AS TrailType,
                        CASE WHEN ISJSON(t.[OldValues]) = 1
                             THEN COALESCE(JSON_VALUE(t.[OldValues], '$.Status'), JSON_VALUE(t.[OldValues], '$.status')) END AS OldStatusRaw,
                        CASE WHEN ISJSON(t.[NewValues]) = 1
                             THEN COALESCE(JSON_VALUE(t.[NewValues], '$.Status'), JSON_VALUE(t.[NewValues], '$.status')) END AS NewStatusRaw
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] = 'Project'
                ),
                StatusRows AS
                (
                    SELECT
                        CAST(e.ProjectIdRaw AS uniqueidentifier) AS ProjectId,
                        e.ChangedByUserId,
                        e.ChangedOn,
                        -- A Create row records entry into the initial state, so it has no prior status.
                        CASE WHEN e.TrailType = 'Create' THEN NULL ELSE e.OldStatusRaw END AS FromRaw,
                        e.NewStatusRaw AS ToRaw
                    FROM Extracted e
                    WHERE e.ProjectIdRaw IS NOT NULL
                      AND e.NewStatusRaw IS NOT NULL
                ),
                Mapped AS
                (
                    SELECT
                        s.ProjectId,
                        s.ChangedByUserId,
                        s.ChangedOn,
                        CASE LOWER(s.FromRaw)
                            WHEN 'proposed'  THEN 'Proposed'
                            WHEN 'approved'  THEN 'Approved'
                            WHEN 'active'    THEN 'Active'
                            WHEN 'completed' THEN 'Completed'
                            -- Both spellings are accepted. A rename migration exists, but this must not
                            -- depend on having caught every row.
                            WHEN 'cancelled' THEN 'Canceled'
                            WHEN 'canceled'  THEN 'Canceled'
                        END AS FromStatus,
                        CASE LOWER(s.ToRaw)
                            WHEN 'proposed'  THEN 'Proposed'
                            WHEN 'approved'  THEN 'Approved'
                            WHEN 'active'    THEN 'Active'
                            WHEN 'completed' THEN 'Completed'
                            WHEN 'cancelled' THEN 'Canceled'
                            WHEN 'canceled'  THEN 'Canceled'
                        END AS ToStatus
                    FROM StatusRows s
                )
                INSERT INTO [Ppm].[ProjectStatusHistory]
                    ([Id], [ProjectId], [FromStatus], [ToStatus], [ChangedByUserId], [ChangedByEmployeeId], [ChangedOn], [Source], [Reason])
                SELECT
                    NEWID(),
                    m.ProjectId,
                    m.FromStatus,
                    m.ToStatus,
                    m.ChangedByUserId,
                    -- Frozen at seed time, exactly as the runtime path freezes it: the user-to-employee
                    -- link is mutable, so resolving it on read would silently rewrite history. Null when
                    -- the acting user has no linked employee.
                    u.[EmployeeId],
                    m.ChangedOn,
                    'Reconstructed',
                    NULL
                FROM Mapped m
                -- Orphan trails exist for deleted projects; without this they would FK-fault the insert.
                INNER JOIN [Ppm].[Projects] p ON p.[Id] = m.ProjectId
                -- Identity is a reserved word, hence the brackets.
                LEFT JOIN [Identity].[Users] u ON u.[Id] = m.ChangedByUserId
                WHERE m.ToStatus IS NOT NULL
                  -- A history row must represent movement; the domain forbids from = to.
                  AND (m.FromStatus IS NULL OR m.FromStatus <> m.ToStatus)
                  -- Makes re-running a no-op.
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [Ppm].[ProjectStatusHistory] h
                      WHERE h.[ProjectId] = m.ProjectId
                        AND h.[ChangedOn] = m.ChangedOn
                        AND h.[ToStatus] = m.ToStatus
                  );

                -- Some projects have no status-bearing audit at all — the trail predates auditing, or
                -- was pruned. They get one synthesized row so the timeline is never empty. The timestamp
                -- is the project's own creation, which is the only defensible stamp available; Source
                -- records that this row was inferred rather than observed.
                INSERT INTO [Ppm].[ProjectStatusHistory]
                    ([Id], [ProjectId], [FromStatus], [ToStatus], [ChangedByUserId], [ChangedByEmployeeId], [ChangedOn], [Source], [Reason])
                SELECT
                    -- NEWID() is safe here, unlike the reconstruction above: there is no originating
                    -- trail row to borrow an Id from, and a synthesized project gets exactly one row,
                    -- so it can never tie with a sibling on ChangedOn.
                    NEWID(),
                    p.[Id],
                    NULL,
                    p.[Status],
                    '{SystemUserId}',
                    NULL,
                    p.[SystemCreated],
                    'Synthesized',
                    NULL
                FROM [Ppm].[Projects] p
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [Ppm].[ProjectStatusHistory] h
                    WHERE h.[ProjectId] = p.[Id]
                );
            ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Source is the handle: this migration writes only Reconstructed and Synthesized rows, and the
        // aggregate writes only Recorded ones, so anything the application has captured since survives.
        migrationBuilder.Sql(@"
                DELETE FROM [Ppm].[ProjectStatusHistory]
                WHERE [Source] IN ('Reconstructed', 'Synthesized');
            ");
    }
}
