using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRoadmapBaselineActivity : Migration
    {
        // Gives every roadmap that has no creation entry in the activity log a baseline, exactly as
        // Backfill-Baseline-Activity did for the first evented aggregates, and removes whatever entries such a roadmap
        // already had. A roadmap with a creation or baseline entry is left as it is, which is also what makes a second
        // run a no-op.
        //
        // Each payload is written by hand, frozen at the shape RoadmapBaselinedEvent has when this ships, and has to
        // deserialize into that type exactly as the serializer would have written it. RoadmapBaselineBackfillTests
        // compares the row against the entry ActivityLogEntryFactory builds for the same event.
        //
        // EventId is BaselineEventId.For: a version 5 UUID of "{AggregateType}:{id}" in the baseline namespace.
        // SQL Server reads the first three groups of a uniqueidentifier's bytes little-endian, so the big-endian hash
        // is reordered before the cast.

        // SystemUser.Id and the baseline namespace, frozen here: a migration must keep writing what it wrote when it
        // shipped.
        private const string SystemUserId = "11111111-1111-1111-1111-111111111111";

        private const string BaselineNamespace = "0x5B0E7C2A3F6D4C8E9A1B7D2F4E6A8C30";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes the baselines. The entries this migration cleared are gone and do not come back.
            migrationBuilder.Sql("""
                DELETE FROM [App].[ActivityLogs]
                WHERE [Category] = 'Baseline'
                  AND [EventType] = 'RoadmapBaselinedEvent';
                """);
        }

        internal static string UpSql => $$"""
            DECLARE @Now datetime2(7) = SYSUTCDATETIME();

            CREATE TABLE #Baseline
            (
                [AggregateId] uniqueidentifier NOT NULL PRIMARY KEY,
                [Body] nvarchar(max) NULL,
                [RecordCreated] datetime2 NULL,
                [RecordCreatedBy] nvarchar(450) NULL
            );

            INSERT INTO #Baseline
            SELECT r.[Id],
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("r.[Id]")}}
                    + N',"key":' + {{Int("r.[Key]")}}
                    + N',"name":' + {{Str("r.[Name]")}}
                    + N',"description":' + {{Str("r.[Description]")}}
                    + N',"dateRange":' + {{DateRange("r.[Start]", "r.[End]")}}
                    + N',"visibility":' + {{EnumName("r.[Visibility]")}}
                    + N',"state":' + {{EnumName("r.[State]")}}
                    + N',"managerIds":[' + COALESCE((
                        SELECT STRING_AGG(CAST({{Guid("m.[ManagerId]")}} AS nvarchar(max)), N',')
                            WITHIN GROUP (ORDER BY m.[ManagerId])
                        FROM [Planning].[RoadmapManagers] m
                        WHERE m.[RoadmapId] = r.[Id]), N'') + N']'
                    -- Colors are the owned JSON column, keyed by EF's property names.
                    + N',"colors":[' + COALESCE((
                        SELECT STRING_AGG(
                            CAST(N'{"color":' AS nvarchar(max)) + {{Str("JSON_VALUE(c.[value], '$.Color')")}}
                                + N',"name":' + {{Str("JSON_VALUE(c.[value], '$.Name')")}}
                                + N',"order":' + {{Int("CAST(JSON_VALUE(c.[value], '$.Order') AS int)")}}
                                + N',"isDefault":' + CASE WHEN JSON_VALUE(c.[value], '$.IsDefault') = 'true' THEN N'true' ELSE N'false' END
                                + N'}',
                            N',') WITHIN GROUP (ORDER BY CAST(JSON_VALUE(c.[value], '$.Order') AS int), CAST(c.[key] AS int))
                        FROM OPENJSON(r.[Colors]) c), N'') + N']'
                    -- A milestone keeps its date in Start and has no End; its range is that single day.
                    + N',"items":[' + COALESCE((
                        SELECT STRING_AGG(
                            CAST(N'{"itemId":' AS nvarchar(max)) + {{Guid("i.[Id]")}}
                                + N',"type":' + CASE i.[Type] WHEN 1 THEN N'"activity"' WHEN 2 THEN N'"milestone"' WHEN 3 THEN N'"timebox"' END
                                + N',"name":' + {{Str("i.[Name]")}}
                                + N',"description":' + {{Str("i.[Description]")}}
                                + N',"parentId":' + {{Guid("i.[ParentId]")}}
                                + N',"color":' + {{Str("i.[Color]")}}
                                + N',"dateRange":' + {{DateRange("i.[Start]", "COALESCE(i.[End], i.[Start])")}}
                                + N',"order":' + CASE WHEN i.[Type] = 1 THEN {{Int("i.[Order]")}} ELSE N'null' END
                                + N'}',
                            N',') WITHIN GROUP (ORDER BY i.[Type], i.[Order], i.[Start], i.[Id])
                        FROM [Planning].[RoadmapItems] i
                        WHERE i.[RoadmapId] = r.[Id]), N'') + N']',
                r.[SystemCreated], r.[SystemCreatedBy]
            FROM [Planning].[Roadmaps] r
            WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateId] = r.[Id] AND a.[AggregateType] = 'Roadmap'
                                AND a.[EventType] IN ('RoadmapCreatedEvent', 'RoadmapBaselinedEvent'));

            DECLARE @Unbuilt int = (SELECT COUNT(*) FROM #Baseline WHERE [Body] IS NULL);
            IF @Unbuilt > 0
            BEGIN
                DECLARE @Message nvarchar(200) = CONCAT(
                    'Backfill-Roadmap-Baseline-Activity could not build ', @Unbuilt, ' baseline payload(s).');
                THROW 51000, @Message, 1;
            END;

            DELETE a
            FROM [App].[ActivityLogs] a
            INNER JOIN #Baseline b
                ON a.[AggregateType] = 'Roadmap'
                AND b.[AggregateId] = a.[AggregateId];

            INSERT INTO [App].[ActivityLogs]
            (
                [EventId], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary]
            )
            SELECT
                id.[EventId],
                'RoadmapBaselinedEvent',
                'Baseline',
                '1.0',
                'Planning',
                'Roadmap',
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
                N'Roadmap Baselined'
            FROM #Baseline b
            LEFT JOIN [Identity].[Users] u
                ON u.[Id] = b.[RecordCreatedBy]
            CROSS APPLY
            (
                SELECT HASHBYTES('SHA1', {{BaselineNamespace}}
                    + CAST('Roadmap:' + LOWER(CONVERT(varchar(36), b.[AggregateId])) AS varbinary(200))) AS [Hash]
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

        private static string Str(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + STRING_ESCAPE({column}, 'json') + N'\"' END";

        private static string Guid(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + LOWER(CONVERT(nvarchar(36), {column})) + N'\"' END";

        private static string Int(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE CONVERT(nvarchar(11), {column}) END";

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
