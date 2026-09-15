using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillScoringModelBaselineActivity : Migration
    {
        // Gives every scoring model that has no creation entry in the activity log a baseline, exactly as
        // Backfill-Baseline-Activity did for the first evented aggregates, and removes whatever entries such a model
        // already had. A model with a creation or baseline entry is left as it is, which is also what makes a second
        // run a no-op.
        //
        // Each payload is written by hand, frozen at the shape ScoringModelBaselinedEvent has when this ships, and has
        // to deserialize into that type exactly as the serializer would have written it.
        // BackfillScoringModelBaselineActivityMigrationTests compares the row against the entry ActivityLogEntryFactory
        // builds for the same event.
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
                  AND [EventType] = 'ScoringModelBaselinedEvent';
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

            -- Built first: SQL Server cannot aggregate an expression that contains another aggregate.
            SELECT l.[ScoringScaleId] AS [ScaleId],
                STRING_AGG(
                    CAST(N'{"levelId":' AS nvarchar(max)) + {{Guid("l.[Id]")}}
                        + N',"label":' + {{Str("l.[Label]")}}
                        + N',"value":' + {{Decimal("l.[Value]")}}
                        + N',"order":' + {{Int("l.[Order]")}}
                        + N'}',
                    N',') WITHIN GROUP (ORDER BY l.[Order], l.[Id]) AS [Levels]
            INTO #ScaleLevels
            FROM [App].[ScoringRatingLevels] l
            GROUP BY l.[ScoringScaleId];

            INSERT INTO #Baseline
            SELECT m.[Id],
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("m.[Id]")}}
                    + N',"key":' + {{Int("m.[Key]")}}
                    + N',"name":' + {{Str("m.[Name]")}}
                    + N',"description":' + {{Str("m.[Description]")}}
                    + N',"scales":[' + COALESCE((
                        SELECT STRING_AGG(
                            CAST(N'{"scaleId":' AS nvarchar(max)) + {{Guid("s.[Id]")}}
                                + N',"name":' + {{Str("s.[Name]")}}
                                + N',"order":' + {{Int("s.[Order]")}}
                                + N',"levels":[' + COALESCE(sl.[Levels], N'') + N']'
                                + N'}',
                            N',') WITHIN GROUP (ORDER BY s.[Order], s.[Id])
                        FROM [App].[ScoringScales] s
                        LEFT JOIN #ScaleLevels sl
                            ON sl.[ScaleId] = s.[Id]
                        WHERE s.[ScoringModelId] = m.[Id]), N'') + N']'
                    + N',"criteria":[' + COALESCE((
                        SELECT STRING_AGG(
                            CAST(N'{"criterionId":' AS nvarchar(max)) + {{Guid("c.[Id]")}}
                                + N',"name":' + {{Str("c.[Name]")}}
                                + N',"token":' + {{Str("c.[Token]")}}
                                + N',"description":' + {{Str("c.[Description]")}}
                                + N',"weight":' + {{Decimal("c.[Weight]")}}
                                + N',"scaleId":' + {{Guid("c.[ScaleId]")}}
                                + N',"order":' + {{Int("c.[Order]")}}
                                + N'}',
                            N',') WITHIN GROUP (ORDER BY c.[Order], c.[Id])
                        FROM [App].[ScoringModelCriteria] c
                        WHERE c.[ScoringModelId] = m.[Id]), N'') + N']'
                    + N',"outputs":[' + COALESCE((
                        SELECT STRING_AGG(
                            CAST(N'{"outputId":' AS nvarchar(max)) + {{Guid("o.[Id]")}}
                                + N',"name":' + {{Str("o.[Name]")}}
                                + N',"token":' + {{Str("o.[Token]")}}
                                + N',"formula":' + {{Str("o.[Formula]")}}
                                + N',"isPrimary":' + {{Bool("o.[IsPrimary]")}}
                                + N',"order":' + {{Int("o.[Order]")}}
                                + N'}',
                            N',') WITHIN GROUP (ORDER BY o.[Order], o.[Id])
                        FROM [App].[ScoringModelOutputs] o
                        WHERE o.[ScoringModelId] = m.[Id]), N'') + N']',
                m.[SystemCreated], m.[SystemCreatedBy]
            FROM [App].[ScoringModels] m
            WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateId] = m.[Id] AND a.[AggregateType] = 'ScoringModel'
                                AND a.[EventType] IN ('ScoringModelCreatedEvent', 'ScoringModelBaselinedEvent'));

            DECLARE @Unbuilt int = (SELECT COUNT(*) FROM #Baseline WHERE [Body] IS NULL);
            IF @Unbuilt > 0
            BEGIN
                DECLARE @Message nvarchar(200) = CONCAT(
                    'Backfill-Scoring-Model-Baseline-Activity could not build ', @Unbuilt, ' baseline payload(s).');
                THROW 51000, @Message, 1;
            END;

            DELETE a
            FROM [App].[ActivityLogs] a
            INNER JOIN #Baseline b
                ON a.[AggregateType] = 'ScoringModel'
                AND b.[AggregateId] = a.[AggregateId];

            INSERT INTO [App].[ActivityLogs]
            (
                [Id], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary]
            )
            SELECT
                id.[EventId],
                'ScoringModelBaselinedEvent',
                'Baseline',
                '1.0',
                'Scoring',
                'ScoringModel',
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
                N'Scoring Model Baselined'
            FROM #Baseline b
            LEFT JOIN [Identity].[Users] u
                ON u.[Id] = b.[RecordCreatedBy]
            CROSS APPLY
            (
                SELECT HASHBYTES('SHA1', {{BaselineNamespace}}
                    + CAST('ScoringModel:' + LOWER(CONVERT(varchar(36), b.[AggregateId])) AS varbinary(200))) AS [Hash]
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
            DROP TABLE #ScaleLevels;
            """;

        private static string Str(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + STRING_ESCAPE({column}, 'json') + N'\"' END";

        private static string Guid(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + LOWER(CONVERT(nvarchar(36), {column})) + N'\"' END";

        private static string Int(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE CONVERT(nvarchar(11), {column}) END";

        /// <summary>A decimal at its column's scale, which JSON reads back as the same decimal.</summary>
        private static string Decimal(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE CONVERT(nvarchar(40), {column}) END";

        private static string Bool(string column) => $"CASE WHEN {column} = 1 THEN N'true' ELSE N'false' END";

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
