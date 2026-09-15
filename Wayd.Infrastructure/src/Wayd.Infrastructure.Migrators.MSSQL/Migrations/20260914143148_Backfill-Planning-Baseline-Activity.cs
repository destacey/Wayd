using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPlanningBaselineActivity : Migration
    {
        // Gives every planning interval, planning interval objective and risk that has no creation entry in the
        // activity log a baseline, exactly as Backfill-Baseline-Activity did for the first nine evented aggregates,
        // and removes whatever entries such a record already had. A record with a creation or baseline entry is left
        // as it is, which is also what makes a second run a no-op. Soft-deleted records are skipped, and so are an
        // interval's soft-deleted iterations and the sprint mappings that still point at them.
        //
        // Each payload is written by hand, frozen at the shape its baseline type has when this ships, and has to
        // deserialize into that type exactly as the serializer would have written it, including a date range's
        // computed days. BackfillPlanningBaselineActivityMigrationTests compares every aggregate's row against the
        // entry ActivityLogEntryFactory builds for the same event.
        //
        // EventId is BaselineEventId.For: a version 5 UUID of "{AggregateType}:{id}" in the baseline namespace.
        // SQL Server reads the first three groups of a uniqueidentifier's bytes little-endian, so the big-endian hash
        // is reordered before the cast.

        // SystemUser.Id and the baseline namespace, frozen here: a migration must keep writing what it wrote when it
        // shipped.
        private const string SystemUserId = "11111111-1111-1111-1111-111111111111";

        private const string BaselineNamespace = "0x5B0E7C2A3F6D4C8E9A1B7D2F4E6A8C30";

        private static readonly string[] BaselineEventTypes =
        [
            "PlanningIntervalBaselinedEvent", "PlanningIntervalObjectiveBaselinedEvent", "RiskBaselinedEvent",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes the baselines. The entries this migration cleared are gone and do not come back.
            migrationBuilder.Sql($"""
                DELETE FROM [App].[ActivityLogs]
                WHERE [Category] = 'Baseline'
                  AND [EventType] IN ({string.Join(", ", BaselineEventTypes.Select(t => $"'{t}'"))});
                """);
        }

        internal static string UpSql => $$"""
            DECLARE @Now datetime2(7) = SYSUTCDATETIME();

            CREATE TABLE #Baseline
            (
                [AggregateType] varchar(64) NOT NULL,
                [AggregateId] uniqueidentifier NOT NULL,
                [EventType] varchar(128) NOT NULL,
                [Summary] nvarchar(512) NOT NULL,
                [Body] nvarchar(max) NULL,
                [RecordCreated] datetime2 NULL,
                [RecordCreatedBy] nvarchar(450) NULL,
                PRIMARY KEY ([AggregateType], [AggregateId])
            );

            -- Planning interval
            INSERT INTO #Baseline
            SELECT 'PlanningInterval', p.[Id], 'PlanningIntervalBaselinedEvent', N'Planning Interval Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("p.[Id]")}}
                    + N',"key":' + {{Int("p.[Key]")}}
                    + N',"name":' + {{Str("p.[Name]")}}
                    + N',"description":' + {{Str("p.[Description]")}}
                    + N',"dateRange":' + {{DateRange("p.[Start]", "p.[End]")}}
                    + N',"objectivesLocked":' + {{Bool("p.[ObjectivesLocked]")}}
                    + N',"iterations":[' + COALESCE((
                        SELECT STRING_AGG(
                            CAST(N'{"iterationId":' AS nvarchar(max)) + {{Guid("i.[Id]")}}
                                + N',"name":' + {{Str("i.[Name]")}}
                                + N',"category":' + {{EnumName("i.[Category]")}}
                                + N',"dateRange":' + {{DateRange("i.[Start]", "i.[End]")}}
                                + N'}',
                            N',') WITHIN GROUP (ORDER BY i.[Start])
                        FROM [Planning].[PlanningIntervalIterations] i
                        WHERE i.[PlanningIntervalId] = p.[Id] AND i.[IsDeleted] = 0), N'') + N']'
                    + N',"teamIds":[' + COALESCE((
                        SELECT STRING_AGG(CAST(N'"' + LOWER(CONVERT(nvarchar(36), t.[TeamId])) + N'"' AS nvarchar(max)), N',')
                            WITHIN GROUP (ORDER BY t.[TeamId])
                        FROM [Planning].[PlanningIntervalTeams] t
                        WHERE t.[PlanningIntervalId] = p.[Id]), N'') + N']'
                    + N',"sprintMappings":[' + COALESCE((
                        SELECT STRING_AGG(
                            CAST(N'{"iterationId":' AS nvarchar(max)) + {{Guid("s.[PlanningIntervalIterationId]")}}
                                + N',"sprintId":' + {{Guid("s.[SprintId]")}}
                                + N'}',
                            N',') WITHIN GROUP (ORDER BY s.[PlanningIntervalIterationId], s.[SprintId])
                        FROM [Planning].[PlanningIntervalIterationSprints] s
                        INNER JOIN [Planning].[PlanningIntervalIterations] si
                            ON si.[Id] = s.[PlanningIntervalIterationId] AND si.[IsDeleted] = 0
                        WHERE s.[PlanningIntervalId] = p.[Id]), N'') + N']',
                p.[SystemCreated], p.[SystemCreatedBy]
            FROM [Planning].[PlanningIntervals] p
            WHERE p.[IsDeleted] = 0
              AND {{NotRecorded("PlanningInterval", "p.[Id]", "PlanningIntervalCreatedEvent", "PlanningIntervalBaselinedEvent")}};

            -- Planning interval objective
            INSERT INTO #Baseline
            SELECT 'PlanningIntervalObjective', o.[Id], 'PlanningIntervalObjectiveBaselinedEvent', N'Planning Interval Objective Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("o.[Id]")}}
                    + N',"key":' + {{Int("o.[Key]")}}
                    + N',"planningIntervalId":' + {{Guid("o.[PlanningIntervalId]")}}
                    + N',"teamId":' + {{Guid("o.[TeamId]")}}
                    + N',"name":' + {{Str("o.[Name]")}}
                    + N',"description":' + {{Str("o.[Description]")}}
                    + N',"type":' + {{EnumName("o.[Type]")}}
                    + N',"status":' + {{EnumName("o.[Status]")}}
                    + N',"progress":' + {{Float("o.[Progress]")}}
                    + N',"isStretch":' + {{Bool("o.[IsStretch]")}}
                    + N',"startDate":' + {{Date("o.[StartDate]")}}
                    + N',"targetDate":' + {{Date("o.[TargetDate]")}}
                    + N',"closedDate":' + {{Instant("o.[ClosedDate]")}}
                    + N',"order":' + {{Int("o.[Order]")}},
                o.[SystemCreated], o.[SystemCreatedBy]
            FROM [Planning].[PlanningIntervalObjectives] o
            WHERE o.[IsDeleted] = 0
              AND {{NotRecorded("PlanningIntervalObjective", "o.[Id]", "PlanningIntervalObjectiveCreatedEvent", "PlanningIntervalObjectiveBaselinedEvent")}};

            -- Risk
            INSERT INTO #Baseline
            SELECT 'Risk', r.[Id], 'RiskBaselinedEvent', N'Risk Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("r.[Id]")}}
                    + N',"key":' + {{Int("r.[Key]")}}
                    + N',"summary":' + {{Str("r.[Summary]")}}
                    + N',"description":' + {{Str("r.[Description]")}}
                    + N',"teamId":' + {{Guid("r.[TeamId]")}}
                    + N',"reportedOn":' + {{Instant("r.[ReportedOn]")}}
                    + N',"reportedById":' + {{Guid("r.[ReportedById]")}}
                    + N',"status":' + {{EnumName("r.[Status]")}}
                    + N',"category":' + {{EnumName("r.[Category]")}}
                    + N',"impact":' + {{EnumName("r.[Impact]")}}
                    + N',"likelihood":' + {{EnumName("r.[Likelihood]")}}
                    + N',"assigneeId":' + {{Guid("r.[AssigneeId]")}}
                    + N',"followUpDate":' + {{Date("r.[FollowUpDate]")}}
                    + N',"response":' + {{Str("r.[Response]")}}
                    + N',"closedDate":' + {{Instant("r.[ClosedDate]")}},
                r.[SystemCreated], r.[SystemCreatedBy]
            FROM [Planning].[Risks] r
            WHERE r.[IsDeleted] = 0
              AND {{NotRecorded("Risk", "r.[Id]", "RiskCreatedEvent", "RiskBaselinedEvent")}};

            DECLARE @Unbuilt int = (SELECT COUNT(*) FROM #Baseline WHERE [Body] IS NULL);
            IF @Unbuilt > 0
            BEGIN
                DECLARE @Message nvarchar(200) = CONCAT(
                    'Backfill-Planning-Baseline-Activity could not build ', @Unbuilt, ' baseline payload(s).');
                THROW 51000, @Message, 1;
            END;

            DELETE a
            FROM [App].[ActivityLogs] a
            INNER JOIN #Baseline b
                ON b.[AggregateType] = a.[AggregateType]
                AND b.[AggregateId] = a.[AggregateId];

            INSERT INTO [App].[ActivityLogs]
            (
                [Id], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary]
            )
            SELECT
                id.[EventId],
                b.[EventType],
                'Baseline',
                '1.0',
                'Planning',
                b.[AggregateType],
                b.[AggregateId],
                'System',
                '{{SystemUserId}}',
                NULL,
                @Now,
                0,
                NULL,
                b.[Body]
                    -- A SystemCreated older than any real record is the column's default, not a creation date.
                    + N',"recordCreatedOn":' + CASE WHEN b.[RecordCreated] IS NULL OR b.[RecordCreated] < '1900-01-01'
                        THEN N'null' ELSE N'"' + {{Iso("b.[RecordCreated]")}} + N'"' END
                    + N',"recordCreatedById":' + {{Guid("u.[EmployeeId]")}}
                    + N',"timestamp":"' + {{Iso("@Now")}} + N'"'
                    + N',"eventId":"' + LOWER(CONVERT(nvarchar(36), id.[EventId])) + N'"'
                    + N',"actor":{"kind":"system","userId":"{{SystemUserId}}","employeeId":null}'
                    + N',"eventVersion":"1.0"}',
                b.[Summary]
            FROM #Baseline b
            LEFT JOIN [Identity].[Users] u
                ON u.[Id] = b.[RecordCreatedBy]
            CROSS APPLY
            (
                SELECT HASHBYTES('SHA1', {{BaselineNamespace}}
                    + CAST(b.[AggregateType] + ':' + LOWER(CONVERT(varchar(36), b.[AggregateId])) AS varbinary(200))) AS [Hash]
            ) h
            CROSS APPLY
            (
                SELECT SUBSTRING(h.[Hash], 1, 6)
                    + CAST(((CAST(SUBSTRING(h.[Hash], 7, 1) AS int) & 15) | 80) AS binary(1))
                    + SUBSTRING(h.[Hash], 8, 1)
                    + CAST(((CAST(SUBSTRING(h.[Hash], 9, 1) AS int) & 63) | 128) AS binary(1))
                    + SUBSTRING(h.[Hash], 10, 7) AS [Uuid]
            ) v
            CROSS APPLY
            (
                SELECT CAST(
                    SUBSTRING(v.[Uuid], 4, 1) + SUBSTRING(v.[Uuid], 3, 1) + SUBSTRING(v.[Uuid], 2, 1) + SUBSTRING(v.[Uuid], 1, 1)
                    + SUBSTRING(v.[Uuid], 6, 1) + SUBSTRING(v.[Uuid], 5, 1)
                    + SUBSTRING(v.[Uuid], 8, 1) + SUBSTRING(v.[Uuid], 7, 1)
                    + SUBSTRING(v.[Uuid], 9, 8) AS uniqueidentifier) AS [EventId]
            ) id;

            DROP TABLE #Baseline;
            """;

        private static string NotRecorded(string aggregateType, string id, string createdEventType, string baselineEventType) => $"""
            NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                        WHERE a.[AggregateId] = {id} AND a.[AggregateType] = '{aggregateType}' AND a.[EventType] IN ('{createdEventType}', '{baselineEventType}'))
            """;

        private static string Str(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + STRING_ESCAPE({column}, 'json') + N'\"' END";

        private static string Guid(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + LOWER(CONVERT(nvarchar(36), {column})) + N'\"' END";

        private static string Int(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE CONVERT(nvarchar(11), {column}) END";

        /// <summary>A float, in the lossless style: 17 significant digits with an exponent, which JSON reads as the same double.</summary>
        private static string Float(string column) => $"CONVERT(nvarchar(30), {column}, 3)";

        private static string Bool(string column) => $"CASE WHEN {column} = 1 THEN N'true' ELSE N'false' END";

        private static string Date(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + CONVERT(nchar(10), {column}, 23) + N'\"' END";

        private static string Instant(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + {Iso(column)} + N'\"' END";

        /// <summary>An enum stored by name, as JsonStringEnumConverter writes it: camelCased.</summary>
        private static string EnumName(string column) =>
            $"N'\"' + LOWER(LEFT({column}, 1)) + SUBSTRING({column}, 2, 64) + N'\"'";

        /// <summary>NodaTime's ExtendedIso pattern: trailing zeros trimmed from the fraction, which disappears with them.</summary>
        private static string Iso(string expression)
        {
            var raw = $"CONVERT(varchar(27), {expression}, 126)";
            var fraction = $"SUBSTRING({raw}, CHARINDEX('.', {raw}) + 1, 7)";
            return $"""
                (CASE WHEN CHARINDEX('.', {raw}) = 0 THEN {raw}
                      WHEN {fraction} NOT LIKE '%[1-9]%' THEN LEFT({raw}, CHARINDEX('.', {raw}) - 1)
                      ELSE LEFT({raw}, CHARINDEX('.', {raw})) + LEFT({fraction}, LEN({fraction}) - PATINDEX('%[^0]%', REVERSE({fraction})) + 1)
                 END + 'Z')
                """;
        }

        /// <summary>A LocalDateRange, with the days the serializer computes.</summary>
        private static string DateRange(string start, string end) => $"""
            (N'{"{"}"start":"' + CONVERT(nchar(10), {start}, 23) + N'","end":"' + CONVERT(nchar(10), {end}, 23)
                + N'","days":' + CONVERT(nvarchar(11), DATEDIFF(DAY, {start}, {end}) + 1) + N'{"}"}')
            """;
    }
}
