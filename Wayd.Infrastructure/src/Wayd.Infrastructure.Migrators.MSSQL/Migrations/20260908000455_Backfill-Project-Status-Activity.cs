using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillProjectStatusActivity : Migration
    {
        // Replays every recorded project status transition into the activity log as a
        // ProjectStatusChangedEvent entry. Transitions were written to ProjectStatusHistory long before
        // they raised an event, so without this a project's activity begins the day the event shipped and
        // its delivery history is missing from the one place that claims to show it. The activity log is
        // intended to supersede ProjectStatusHistory, so every column of that table has to be recoverable
        // from these entries before it can be retired.
        //
        // The payload is written by hand rather than by the serializer, which is the point of doing this
        // once in a migration: it is frozen at the shape ProjectStatusChangedEvent had when this shipped
        // (eventVersion 1.0). A field added to the event later must not retroactively change how an old
        // transition was recorded.
        //
        // Idempotent, and permanently so: the event takes the id of the history row that recorded the
        // transition, so a replayed entry and a live one for the same transition are the same row rather
        // than two. That is what the NOT EXISTS guard leans on.
        //
        // Needs SQL Server 2016 or later, for STRING_ESCAPE.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                WITH StatusCategory([Status], [Category]) AS
                (
                    -- LifecycleCategory, camelCased exactly as JsonStringEnumConverter writes it. Mirrors
                    -- the Display GroupName on each ProjectStatus, which is where
                    -- ProjectStatusLifecycle.CategoryOf reads it from.
                    SELECT * FROM (VALUES
                        ('Proposed',  'notStarted'),
                        ('Approved',  'notStarted'),
                        ('Active',    'active'),
                        ('Completed', 'completed'),
                        ('Canceled',  'canceled')
                    ) v([Status], [Category])
                ),
                BackwardTransition([FromStatus], [ToStatus]) AS
                (
                    -- ProjectStatusLifecycle.BackwardTargets, expanded. An explicit table for the same
                    -- reason it is one in the domain: ProjectStatus values are not in lifecycle order.
                    SELECT * FROM (VALUES
                        ('Completed', 'Proposed'), ('Completed', 'Approved'), ('Completed', 'Active'),
                        ('Canceled',  'Proposed'), ('Canceled',  'Approved'), ('Canceled',  'Active'),
                        ('Active',    'Proposed'), ('Active',    'Approved'),
                        ('Approved',  'Proposed')
                    ) v([FromStatus], [ToStatus])
                )
                INSERT INTO [App].[ActivityLogs]
                (
                    [Id], [EventType], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                    [ActorKind], [UserId], [EmployeeId], [Timestamp], [CorrelationId], [Payload], [Summary]
                )
                SELECT
                    h.[Id],
                    'ProjectStatusChangedEvent',
                    '1.0',
                    'Ppm',
                    'Project',
                    h.[ProjectId],
                    actor.[Kind],
                    h.[ChangedByUserId],
                    -- EventActor.System carries no employee, so a system transition drops the employee the
                    -- history row froze. Matches PpmActor.ToEventActor rather than the raw column.
                    CASE WHEN actor.[Kind] = 'System' THEN NULL ELSE h.[ChangedByEmployeeId] END,
                    h.[ChangedOn],
                    -- The request that caused a transition was never recorded, so a replayed entry
                    -- correlates with nothing. Deliberately null rather than invented.
                    NULL,
                    CAST(N'{""id"":""' AS nvarchar(max)) + LOWER(CONVERT(varchar(36), h.[ProjectId])) + N'""'
                        + N',""key"":""' + STRING_ESCAPE(p.[Key], 'json') + N'""'
                        + N',""name"":""' + STRING_ESCAPE(p.[Name], 'json') + N'""'
                        + N',""fromStatus"":' + CASE WHEN h.[FromStatus] IS NULL THEN N'null' ELSE N'""' + h.[FromStatus] + N'""' END
                        + N',""fromCategory"":' + CASE WHEN h.[FromStatus] IS NULL THEN N'null' ELSE N'""' + fromCategory.[Category] + N'""' END
                        + N',""toStatus"":""' + h.[ToStatus] + N'""'
                        + N',""toCategory"":""' + toCategory.[Category] + N'""'
                        + N',""isBackward"":' + CASE WHEN backward.[FromStatus] IS NULL THEN N'false' ELSE N'true' END
                        + N',""source"":""' + h.[Source] + N'""'
                        + N',""reason"":' + CASE WHEN h.[Reason] IS NULL THEN N'null' ELSE N'""' + STRING_ESCAPE(h.[Reason], 'json') + N'""' END
                        + N',""sequence"":' + CONVERT(varchar(11), h.[Sequence])
                        + N',""timestamp"":""' + iso.[Value] + N'""'
                        + N',""eventId"":""' + LOWER(CONVERT(varchar(36), h.[Id])) + N'""'
                        + N',""actor"":{""kind"":""' + actor.[JsonKind] + N'""'
                        + N',""userId"":""' + STRING_ESCAPE(h.[ChangedByUserId], 'json') + N'""'
                        + N',""employeeId"":' + CASE
                            WHEN actor.[Kind] = 'System' OR h.[ChangedByEmployeeId] IS NULL THEN N'null'
                            ELSE N'""' + LOWER(CONVERT(varchar(36), h.[ChangedByEmployeeId])) + N'""'
                          END + N'}'
                        + N',""eventVersion"":""1.0""}',
                    'Project Status Changed'
                FROM [Ppm].[ProjectStatusHistory] h
                INNER JOIN [Ppm].[Projects] p
                    ON p.[Id] = h.[ProjectId]
                INNER JOIN StatusCategory toCategory
                    ON toCategory.[Status] = h.[ToStatus]
                LEFT JOIN StatusCategory fromCategory
                    ON fromCategory.[Status] = h.[FromStatus]
                LEFT JOIN BackwardTransition backward
                    ON backward.[FromStatus] = h.[FromStatus]
                    AND backward.[ToStatus] = h.[ToStatus]
                CROSS APPLY
                (
                    SELECT CASE WHEN h.[ChangedByUserId] = '11111111-1111-1111-1111-111111111111'
                                THEN 'System' ELSE 'User' END AS [Kind]
                ) kind
                CROSS APPLY
                (
                    SELECT kind.[Kind] AS [Kind],
                           CASE kind.[Kind] WHEN 'System' THEN 'system' ELSE 'user' END AS [JsonKind]
                ) actor
                CROSS APPLY
                (
                    -- NodaTime's ExtendedIso pattern, which is what the serializer writes: trailing zeros
                    -- are trimmed from the fraction and the point disappears with them. Style 126 is
                    -- culture-independent, unlike FORMAT.
                    SELECT CONVERT(varchar(27), h.[ChangedOn], 126) AS [Raw]
                ) raw
                CROSS APPLY
                (
                    SELECT
                        CASE WHEN CHARINDEX('.', raw.[Raw]) = 0 THEN raw.[Raw]
                             ELSE LEFT(raw.[Raw], CHARINDEX('.', raw.[Raw]) - 1) END AS [Seconds],
                        CASE WHEN CHARINDEX('.', raw.[Raw]) = 0 THEN ''
                             ELSE SUBSTRING(raw.[Raw], CHARINDEX('.', raw.[Raw]) + 1, 7) END AS [Fraction]
                ) split
                CROSS APPLY
                (
                    SELECT split.[Seconds]
                        + CASE WHEN split.[Fraction] LIKE '%[1-9]%'
                               THEN '.' + LEFT(split.[Fraction], LEN(split.[Fraction]) - PATINDEX('%[^0]%', REVERSE(split.[Fraction])) + 1)
                               ELSE '' END
                        + 'Z' AS [Value]
                ) iso
                WHERE NOT EXISTS
                (
                    SELECT 1 FROM [App].[ActivityLogs] existing WHERE existing.[Id] = h.[Id]
                );
            ");

            // The status-to-category join is an inner one, so a status absent from the table above would
            // drop its rows rather than fail. Silence is the wrong outcome for a migration whose whole
            // purpose is to make the log a complete stand-in for the table, so anything left behind stops
            // the migration instead.
            migrationBuilder.Sql(@"
                DECLARE @Missing int = (
                    SELECT COUNT(*)
                    FROM [Ppm].[ProjectStatusHistory] h
                    INNER JOIN [Ppm].[Projects] p ON p.[Id] = h.[ProjectId]
                    WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a WHERE a.[Id] = h.[Id])
                );

                IF @Missing > 0
                BEGIN
                    DECLARE @Message nvarchar(200) = CONCAT(
                        'Backfill-Project-Status-Activity left ', @Missing,
                        ' status history row(s) unreplayed. A ToStatus is missing from the category table.');
                    THROW 51000, @Message, 1;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes every status entry that has a history row behind it. This necessarily also removes
            // entries a live transition wrote after the backfill, because a replayed entry and a live one
            // deliberately share an id — there is no column that tells them apart. Acceptable only because
            // reverting this migration means reverting to a build from before the event existed.
            migrationBuilder.Sql(@"
                DELETE a
                FROM [App].[ActivityLogs] a
                INNER JOIN [Ppm].[ProjectStatusHistory] h ON h.[Id] = a.[Id]
                WHERE a.[EventType] = 'ProjectStatusChangedEvent';
            ");
        }
    }
}
