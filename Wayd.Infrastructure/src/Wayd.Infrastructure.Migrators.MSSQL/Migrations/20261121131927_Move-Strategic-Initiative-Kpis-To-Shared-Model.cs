using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class MoveStrategicInitiativeKpisToSharedModel : Migration
    {
        // Copies every strategic initiative KPI, with its checkpoints and measurements, into the shared KPI tables
        // under the ppm.strategic-initiative subject type, and gives each copied KPI a baseline, as
        // Backfill-Link-Baseline-Activity did for links. Ids are kept; keys are new, because the shared table numbers
        // its own. The initiative tables are left as they are: what was logged against them stays readable, and a
        // later migration drops them.
        //
        // Measurements were moments, so each KPI takes the point-in-time window and every measurement keeps its moment
        // as MeasuredAt. A checkpoint's moment becomes a calendar day. Both read the day in the system time zone,
        // GeneralSettings.TimeZone, as Kpi.AddMeasurement does. That is an IANA id and SQL Server's AT TIME ZONE takes
        // Windows names only, so the conversion table is built from the runtime's IANA-to-Windows mapping. The old
        // screens stored a picked date as midnight in the browser's zone, so a checkpoint entered from a browser east or
        // west of the system zone can land on the neighbouring day.
        //
        // A KPI is copied once: one already in the shared table, or with a baseline entry, is skipped. The baseline
        // outlives a moved KPI someone has since removed, so a second run never brings that KPI back.
        //
        // The old KPI checked less than the shared one. A KPI the shared model would refuse — a starting value already
        // at its target, a checkpoint at-risk value on the wrong side of its planned value, two checkpoints on one
        // day, more checkpoints than a plan holds — stops the migration with the KPIs named, so the data is corrected
        // through the initiative screens before it moves rather than carried across broken.
        //
        // Each payload is written by hand, frozen at the shape KpiBaselinedEvent has when this ships, and has to
        // deserialize into that type exactly as the serializer would have written it. EventId is BaselineEventId.For,
        // worked out as Backfill-Link-Baseline-Activity explains.

        // SystemUser.Id and the baseline namespace, frozen here: a migration must keep writing what it wrote when it
        // shipped.
        private const string SystemUserId = "11111111-1111-1111-1111-111111111111";

        private const string BaselineNamespace = "0x5B0E7C2A3F6D4C8E9A1B7D2F4E6A8C30";

        // The subject type and its activity aggregate type, as PpmKpiSubjects.StrategicInitiative declares them.
        private const string SubjectType = "ppm.strategic-initiative";

        private const string SubjectActivityType = "StrategicInitiative";

        // KpiCheckpointPlan.MaxCheckpoints when this ships.
        private const int MaxCheckpoints = 104;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removes what Up copied and the baselines it wrote. The initiative tables were never changed.
            migrationBuilder.Sql($$"""
                CREATE TABLE #Moved ([Id] uniqueidentifier NOT NULL PRIMARY KEY);

                INSERT INTO #Moved
                SELECT k.[Id]
                FROM [Kpis].[Kpis] k
                INNER JOIN [Ppm].[StrategicInitiativeKpis] s ON s.[Id] = k.[Id]
                WHERE k.[SubjectType] = '{{SubjectType}}';

                DELETE r
                FROM [App].[ActivityLogRelatedAggregates] r
                INNER JOIN [App].[ActivityLogs] a ON a.[Id] = r.[ActivityLogId]
                INNER JOIN #Moved m ON m.[Id] = a.[AggregateId]
                WHERE a.[AggregateType] = 'Kpi' AND a.[EventType] = 'KpiBaselinedEvent';

                DELETE a
                FROM [App].[ActivityLogs] a
                INNER JOIN #Moved m ON m.[Id] = a.[AggregateId]
                WHERE a.[AggregateType] = 'Kpi' AND a.[EventType] = 'KpiBaselinedEvent';

                DELETE c FROM [Kpis].[Checkpoints] c INNER JOIN #Moved m ON m.[Id] = c.[KpiId];
                DELETE x FROM [Kpis].[Measurements] x INNER JOIN #Moved m ON m.[Id] = x.[KpiId];
                DELETE k FROM [Kpis].[Kpis] k INNER JOIN #Moved m ON m.[Id] = k.[Id];

                DROP TABLE #Moved;
                """);
        }

        internal static string UpSql => $$"""
            SET NOCOUNT ON;

            DECLARE @Now datetime2(7) = SYSUTCDATETIME();

            -- The KPIs not yet moved, which is also what makes a second run a no-op.
            CREATE TABLE #Source
            (
                [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                [InitiativeId] uniqueidentifier NOT NULL,
                [OldKey] int NOT NULL,
                [Order] int NOT NULL
            );

            INSERT INTO #Source
            SELECT k.[Id], k.[StrategicInitiativeId], k.[Key],
                ROW_NUMBER() OVER (PARTITION BY k.[StrategicInitiativeId] ORDER BY k.[Order], k.[Key]) - 1
            FROM [Ppm].[StrategicInitiativeKpis] k
            WHERE NOT EXISTS (SELECT 1 FROM [Kpis].[Kpis] x WHERE x.[Id] = k.[Id])
              AND NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateType] = 'Kpi' AND a.[AggregateId] = k.[Id]
                                AND a.[EventType] = 'KpiBaselinedEvent');

            IF NOT EXISTS (SELECT 1 FROM #Source)
            BEGIN
                DROP TABLE #Source;
                RETURN;
            END;

            {{SystemZoneSql("Move-Strategic-Initiative-Kpis-To-Shared-Model")}}

            -- What the shared model refuses, found before anything is written.
            CREATE TABLE #Problem ([KpiId] uniqueidentifier NOT NULL, [Problem] nvarchar(200) NOT NULL);

            INSERT INTO #Problem
            SELECT k.[Id], N'its starting value is already at its target'
            FROM [Ppm].[StrategicInitiativeKpis] k
            INNER JOIN #Source s ON s.[Id] = k.[Id]
            WHERE (k.[TargetDirection] = 'Increase' AND k.[StartingValue] >= k.[TargetValue])
               OR (k.[TargetDirection] = 'Decrease' AND k.[StartingValue] <= k.[TargetValue]);

            INSERT INTO #Problem
            SELECT k.[Id], N'its target direction is neither Increase nor Decrease'
            FROM [Ppm].[StrategicInitiativeKpis] k
            INNER JOIN #Source s ON s.[Id] = k.[Id]
            WHERE k.[TargetDirection] NOT IN ('Increase', 'Decrease');

            INSERT INTO #Problem
            SELECT DISTINCT k.[Id], N'a checkpoint''s at-risk value is not short of its planned value'
            FROM [Ppm].[StrategicInitiativeKpis] k
            INNER JOIN #Source s ON s.[Id] = k.[Id]
            INNER JOIN [Ppm].[StrategicInitiativeKpiCheckpoints] c ON c.[KpiId] = k.[Id]
            WHERE (k.[TargetDirection] = 'Increase' AND c.[AtRiskValue] >= c.[TargetValue])
               OR (k.[TargetDirection] = 'Decrease' AND c.[AtRiskValue] <= c.[TargetValue]);

            INSERT INTO #Problem
            SELECT DISTINCT c.[KpiId], N'two of its checkpoints fall on one day'
            FROM [Ppm].[StrategicInitiativeKpiCheckpoints] c
            INNER JOIN #Source s ON s.[Id] = c.[KpiId]
            GROUP BY c.[KpiId], {{LocalDay("c.[CheckpointDate]")}}
            HAVING COUNT(*) > 1;

            INSERT INTO #Problem
            SELECT c.[KpiId], N'it has more than {{MaxCheckpoints}} checkpoints'
            FROM [Ppm].[StrategicInitiativeKpiCheckpoints] c
            INNER JOIN #Source s ON s.[Id] = c.[KpiId]
            GROUP BY c.[KpiId]
            HAVING COUNT(*) > {{MaxCheckpoints}};

            IF EXISTS (SELECT 1 FROM #Problem)
            BEGIN
                DECLARE @Problems nvarchar(2000) = (
                    SELECT STRING_AGG(CAST(CONCAT('KPI ', k.[Key], ' on initiative ', i.[Key], ': ', p.[Problem]) AS nvarchar(max)), '; ')
                    FROM (SELECT TOP (20) * FROM #Problem ORDER BY [KpiId], [Problem]) p
                    INNER JOIN [Ppm].[StrategicInitiativeKpis] k ON k.[Id] = p.[KpiId]
                    INNER JOIN [Ppm].[StrategicInitiatives] i ON i.[Id] = k.[StrategicInitiativeId]);
                DECLARE @ProblemMessage nvarchar(2048) = CONCAT(
                    'Move-Strategic-Initiative-Kpis-To-Shared-Model found ', (SELECT COUNT(*) FROM #Problem),
                    ' strategic initiative KPI problem(s) to correct before it can run: ', @Problems);
                THROW 51000, @ProblemMessage, 1;
            END;

            -- Ordered by the old key so the new keys keep the order the KPIs were created in.
            INSERT INTO [Kpis].[Kpis]
            (
                [Id], [SubjectType], [SubjectId], [Name], [Description], [CategoryId], [Prefix], [Suffix], [Direction],
                [BaselineValue], [BaselineWindowStart], [BaselineWindowEnd], [TargetKind], [TargetValue],
                [AtRiskKind], [AtRiskValue], [Source], [ExpressedAs], [RollingAverageOf],
                [WindowKind], [WindowAnchorDate], [WindowRollingCount], [WindowRollingUnit], [Status], [Order],
                [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy]
            )
            SELECT
                k.[Id], '{{SubjectType}}', k.[StrategicInitiativeId], k.[Name], k.[Description], NULL, k.[Prefix],
                k.[Suffix], k.[TargetDirection],
                k.[StartingValue], NULL, NULL, 'Absolute', k.[TargetValue],
                NULL, NULL, 'Manual', CASE WHEN k.[StartingValue] IS NULL THEN 'Value' ELSE 'ProgressToTarget' END, NULL,
                'PointInTime', NULL, NULL, NULL, 'Active', s.[Order],
                k.[SystemCreated], k.[SystemCreatedBy], k.[SystemLastModified], k.[SystemLastModifiedBy]
            FROM [Ppm].[StrategicInitiativeKpis] k
            INNER JOIN #Source s ON s.[Id] = k.[Id]
            ORDER BY k.[Key];

            INSERT INTO [Kpis].[Measurements]
            (
                [Id], [KpiId], [Value], [PeriodStart], [PeriodEnd], [PeriodRecordKind], [PeriodRecordId], [MeasuredAt],
                [MeasuredOn], [MeasuredById], [Source], [SampleSize], [Note],
                [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy]
            )
            SELECT
                m.[Id], m.[KpiId], m.[ActualValue], d.[Day], d.[Day], NULL, NULL, m.[MeasurementDate],
                d.[Day], m.[MeasuredById], 'Manual', NULL, m.[Note],
                m.[SystemCreated], m.[SystemCreatedBy], m.[SystemLastModified], m.[SystemLastModifiedBy]
            FROM [Ppm].[StrategicInitiativeKpiMeasurements] m
            INNER JOIN #Source s ON s.[Id] = m.[KpiId]
            CROSS APPLY (SELECT {{LocalDay("m.[MeasurementDate]")}} AS [Day]) d;

            INSERT INTO [Kpis].[Checkpoints]
            (
                [Id], [KpiId], [Date], [Label], [PlannedValue], [AtRiskValue],
                [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy]
            )
            SELECT
                c.[Id], c.[KpiId], {{LocalDay("c.[CheckpointDate]")}}, LTRIM(RTRIM(c.[DateLabel])), c.[TargetValue],
                c.[AtRiskValue],
                c.[SystemCreated], c.[SystemCreatedBy], c.[SystemLastModified], c.[SystemLastModifiedBy]
            FROM [Ppm].[StrategicInitiativeKpiCheckpoints] c
            INNER JOIN #Source s ON s.[Id] = c.[KpiId];

            -- The baseline names who created the KPI where the initiative's activity recorded a linked employee.
            CREATE TABLE #Baseline
            (
                [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                [InitiativeId] uniqueidentifier NOT NULL,
                [Body] nvarchar(max) NULL,
                [EventId] uniqueidentifier NOT NULL
            );

            INSERT INTO #Baseline
            SELECT k.[Id], k.[SubjectId],
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("k.[Id]")}}
                    + N',"key":' + CONVERT(nvarchar(11), k.[Key])
                    + N',"subjectType":"{{SubjectType}}"'
                    + N',"subjectId":' + {{Guid("k.[SubjectId]")}}
                    + N',"details":{"name":' + {{Str("k.[Name]")}}
                        + N',"description":' + {{Str("k.[Description]")}}
                        + N',"categoryId":null'
                        + N',"prefix":' + {{Str("k.[Prefix]")}}
                        + N',"suffix":' + {{Str("k.[Suffix]")}}
                        + N',"expressedAs":' + CASE k.[ExpressedAs] WHEN 'Value' THEN N'"value"' ELSE N'"progressToTarget"' END
                        + N',"rollingAverageOf":null}'
                    + N',"goal":{"direction":' + CASE k.[Direction] WHEN 'Increase' THEN N'"increase"' ELSE N'"decrease"' END
                        + N',"baseline":{"value":' + {{Num("k.[BaselineValue]")}} + N',"window":null}'
                        + N',"target":{"kind":"absolute","value":' + {{Num("k.[TargetValue]")}} + N'}'
                        + N',"atRisk":null'
                        + N',"targetValue":' + {{Num("k.[TargetValue]")}}
                        + N',"atRiskValue":null}'
                    + N',"source":"manual"'
                    + N',"order":' + CONVERT(nvarchar(11), k.[Order])
                    + N',"window":{"kind":"pointInTime","anchorDate":null,"rollingCount":null,"rollingUnit":null}'
                    + N',"recordCreatedOn":"' + {{Iso("k.[SystemCreated]")}} + N'"'
                    + N',"recordCreatedById":' + {{Guid("creator.[EmployeeId]")}},
                id.[EventId]
            FROM [Kpis].[Kpis] k
            INNER JOIN #Source s ON s.[Id] = k.[Id]
            OUTER APPLY
            (
                SELECT TOP (1) a.[EmployeeId]
                FROM [App].[ActivityLogs] a
                WHERE a.[AggregateType] = '{{SubjectActivityType}}'
                  AND a.[AggregateId] = k.[SubjectId]
                  AND a.[EventType] = 'StrategicInitiativeKpiAddedEvent'
                  AND ISJSON(a.[Payload]) = 1
                  AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(a.[Payload], '$.kpiId')) = k.[Id]
                ORDER BY a.[Timestamp]
            ) creator
            CROSS APPLY
            (
                SELECT HASHBYTES('SHA1', {{BaselineNamespace}}
                    + CAST('Kpi:' + LOWER(CONVERT(varchar(36), k.[Id])) AS varbinary(200))) AS [Hash]
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

            DECLARE @Unbuilt int = (SELECT COUNT(*) FROM #Baseline WHERE [Body] IS NULL);
            IF @Unbuilt > 0
            BEGIN
                DECLARE @UnbuiltMessage nvarchar(200) = CONCAT(
                    'Move-Strategic-Initiative-Kpis-To-Shared-Model could not build ', @Unbuilt, ' baseline payload(s).');
                THROW 51000, @UnbuiltMessage, 1;
            END;

            INSERT INTO [App].[ActivityLogs]
            (
                [EventId], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary]
            )
            SELECT
                b.[EventId],
                'KpiBaselinedEvent',
                'Baseline',
                '1.1',
                'App',
                'Kpi',
                b.[Id],
                'System',
                '{{SystemUserId}}',
                NULL,
                @Now,
                0,
                NULL,
                b.[Body]
                    + N',"timestamp":"' + {{Iso("@Now")}} + N'"'
                    + N',"eventId":"' + LOWER(CONVERT(nvarchar(36), b.[EventId])) + N'"'
                    + N',"actor":{"kind":"system","userId":"{{SystemUserId}}","employeeId":null}'
                    + N',"eventVersion":"1.1"}',
                N'Kpi Baselined'
            FROM #Baseline b
            WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a WHERE a.[EventId] = b.[EventId]);

            INSERT INTO [App].[ActivityLogRelatedAggregates] ([ActivityLogId], [AggregateType], [AggregateId])
            SELECT a.[Id], '{{SubjectActivityType}}', b.[InitiativeId]
            FROM [App].[ActivityLogs] a
            INNER JOIN #Baseline b ON b.[EventId] = a.[EventId]
            WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogRelatedAggregates] r WHERE r.[ActivityLogId] = a.[Id]);

            DROP TABLE #Baseline;
            DROP TABLE #Problem;
            DROP TABLE #Zones;
            DROP TABLE #Source;
            """;

        /// <summary>
        /// Declares <c>@Windows</c>, the system time zone's Windows name, for <see cref="LocalDay"/>, leaving <c>#Zones</c>
        /// to drop. Throws, naming <paramref name="migration"/>, when SQL Server has no zone to match it.
        /// </summary>
        internal static string SystemZoneSql(string migration) => $$"""
            -- The system time zone, as GeneralSettings reads it: UTC when it was never saved.
            DECLARE @Zone nvarchar(128) = COALESCE(
                (SELECT JSON_VALUE(s.[Value], '$.timeZone') FROM [App].[SystemSettings] s
                 WHERE s.[Scope] = 'System' AND s.[Key] = 'general'),
                N'UTC');

            -- Collated as the database is, so the match against the catalog view holds whatever tempdb's collation.
            CREATE TABLE #Zones
            (
                [Iana] nvarchar(128) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
                [Windows] nvarchar(128) COLLATE DATABASE_DEFAULT NOT NULL
            );
            {{ZoneInserts()}}

            DECLARE @Windows nvarchar(128) =
                (SELECT z.[Windows] FROM #Zones z
                 INNER JOIN sys.time_zone_info t ON t.[name] = z.[Windows]
                 WHERE z.[Iana] = @Zone);

            IF @Windows IS NULL
            BEGIN
                DECLARE @ZoneMessage nvarchar(400) = CONCAT(
                    '{{migration}} cannot read days in the system time zone ''', @Zone,
                    ''': SQL Server has no matching time zone.');
                THROW 51000, @ZoneMessage, 1;
            END;
            """;

        /// <summary>
        /// Inserts every IANA zone the runtime can name a Windows zone for. Only the system zone's row is read; the
        /// rest are there because which one that is is not known until the SQL runs.
        /// </summary>
        private static string ZoneInserts()
        {
            var rows = DateTimeZoneProviders.Tzdb.Ids
                .Select(iana => TimeZoneInfo.TryConvertIanaIdToWindowsId(iana, out var windows) ? (iana, windows) : (iana, windows: null))
                .Where(z => z.windows is not null)
                .Append((iana: "UTC", windows: "UTC"))
                .DistinctBy(z => z.iana, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sql = new StringBuilder();
            foreach (var chunk in rows.Chunk(500))
            {
                sql.AppendLine("INSERT INTO #Zones ([Iana], [Windows]) VALUES");
                sql.AppendLine(string.Join(",\n", chunk.Select(z => $"(N'{z.iana.Replace("'", "''")}', N'{z.windows.Replace("'", "''")}')")) + ";");
            }

            return sql.ToString();
        }

        /// <summary>The calendar day an instant column falls on in the system time zone.</summary>
        internal static string LocalDay(string column) =>
            $"CAST((CAST({column} AS datetime2) AT TIME ZONE 'UTC') AT TIME ZONE @Windows AS date)";

        internal static string Str(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + STRING_ESCAPE({column}, 'json') + N'\"' END";

        internal static string Guid(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + LOWER(CONVERT(nvarchar(36), {column})) + N'\"' END";

        /// <summary>A double as the serializer writes it: the shortest form that reads back as the same value.</summary>
        internal static string Num(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE FORMAT({column}, 'R', 'en-US') END";

        /// <summary>NodaTime's ExtendedIso pattern: trailing zeros trimmed from the fraction, which disappears with them.</summary>
        internal static string Iso(string expression)
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
