using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLinkBaselineActivity : Migration
    {
        // Gives every link that has no creation entry a baseline, as Backfill-Intake-Channel-Baseline-Activity did for
        // intake channels. Links raised no event before this, so every link on file has no history. A link with a
        // creation or baseline entry is left as it is, which is also what makes a second run a no-op. A link's
        // baseline is also filed under the record it is on, as LinkBaselinedEvent.RelatedAggregates files it, or the
        // record's Activity would never list it.
        //
        // Each payload is written by hand, frozen at the shape LinkBaselinedEvent has when this ships, and has to
        // deserialize into that type exactly as the serializer would have written it. A link records neither when nor
        // by whom it was created, so both are null.
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
                DELETE r
                FROM [App].[ActivityLogRelatedAggregates] r
                INNER JOIN [App].[ActivityLogs] a
                    ON a.[Id] = r.[ActivityLogId]
                WHERE a.[Category] = 'Baseline'
                  AND a.[EventType] = 'LinkBaselinedEvent';

                DELETE FROM [App].[ActivityLogs]
                WHERE [Category] = 'Baseline'
                  AND [EventType] = 'LinkBaselinedEvent';
                """);
        }

        internal static string UpSql => $$"""
            DECLARE @Now datetime2(7) = SYSUTCDATETIME();

            CREATE TABLE #Baseline
            (
                [AggregateType] varchar(64) NOT NULL,
                [AggregateId] uniqueidentifier NOT NULL,
                [EventType] varchar(128) NOT NULL,
                [DomainArea] varchar(64) NOT NULL,
                [Summary] nvarchar(256) NOT NULL,
                [Body] nvarchar(max) NULL,
                [OwnerAggregateType] varchar(64) NULL,
                [OwnerId] uniqueidentifier NOT NULL,
                PRIMARY KEY ([AggregateType], [AggregateId])
            );

            INSERT INTO #Baseline
            SELECT 'Link', l.[Id], 'LinkBaselinedEvent', 'Links', N'Link Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("l.[Id]")}}
                    + N',"ownerType":' + {{Str("l.[OwnerType]")}}
                    + N',"ownerId":' + {{Guid("l.[OwnerId]")}}
                    + N',"name":' + {{Str("l.[Name]")}}
                    + N',"url":' + {{Str("l.[Url]")}},
                -- The activity type each owner type declares, frozen as the descriptors have it when this ships.
                CASE l.[OwnerType]
                    WHEN 'organization.team' THEN 'Team'
                    WHEN 'planning.planning-interval' THEN 'PlanningInterval'
                    WHEN 'planning.planning-interval-objective' THEN 'PlanningIntervalObjective'
                    WHEN 'planning.risk' THEN 'Risk'
                    WHEN 'ppm.portfolio' THEN 'ProjectPortfolio'
                    WHEN 'ppm.program' THEN 'Program'
                    WHEN 'ppm.project' THEN 'Project'
                    WHEN 'ppm.strategic-initiative' THEN 'StrategicInitiative'
                    WHEN 'product.incident' THEN 'Incident'
                    WHEN 'work.sprint' THEN 'Iteration'
                END,
                l.[OwnerId]
            FROM [Links].[Links] l
            WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateId] = l.[Id] AND a.[AggregateType] = 'Link'
                                AND a.[EventType] IN ('LinkAddedEvent', 'LinkBaselinedEvent'));

            DECLARE @Unbuilt int = (SELECT COUNT(*) FROM #Baseline WHERE [Body] IS NULL);
            IF @Unbuilt > 0
            BEGIN
                DECLARE @Message nvarchar(200) = CONCAT(
                    'Backfill-Link-Baseline-Activity could not build ', @Unbuilt, ' baseline payload(s).');
                THROW 51000, @Message, 1;
            END;

            DELETE a
            FROM [App].[ActivityLogs] a
            INNER JOIN #Baseline b
                ON a.[AggregateType] = b.[AggregateType]
                AND a.[AggregateId] = b.[AggregateId];

            INSERT INTO [App].[ActivityLogs]
            (
                [EventId], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary]
            )
            SELECT
                id.[EventId],
                b.[EventType],
                'Baseline',
                '1.0',
                b.[DomainArea],
                b.[AggregateType],
                b.[AggregateId],
                'System',
                '{{SystemUserId}}',
                NULL,
                @Now,
                0,
                NULL,
                b.[Body]
                    + N',"recordCreatedOn":null'
                    + N',"recordCreatedById":null'
                    + N',"timestamp":"' + {{Iso("@Now")}} + N'"'
                    + N',"eventId":"' + LOWER(CONVERT(nvarchar(36), id.[EventId])) + N'"'
                    + N',"actor":{"kind":"system","userId":"{{SystemUserId}}","employeeId":null}'
                    + N',"eventVersion":"1.0"}',
                b.[Summary]
            FROM #Baseline b
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

            INSERT INTO [App].[ActivityLogRelatedAggregates] ([ActivityLogId], [AggregateType], [AggregateId])
            SELECT a.[Id], b.[OwnerAggregateType], b.[OwnerId]
            FROM [App].[ActivityLogs] a
            INNER JOIN #Baseline b
                ON b.[AggregateType] = a.[AggregateType]
                AND b.[AggregateId] = a.[AggregateId]
            WHERE a.[EventType] = 'LinkBaselinedEvent'
              AND b.[OwnerAggregateType] IS NOT NULL;

            DROP TABLE #Baseline;
            """;

        private static string Str(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + STRING_ESCAPE({column}, 'json') + N'\"' END";

        private static string Guid(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + LOWER(CONVERT(nvarchar(36), {column})) + N'\"' END";

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
    }
}
