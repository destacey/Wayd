using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillBaselineActivity : Migration
    {
        // Gives every record of the nine evented aggregates that has no creation entry in the activity log a
        // baseline, and removes whatever entries that record already had, so its history starts from a known
        // state. A record with a creation or baseline entry is left exactly as it is, which is also what makes
        // a second run a no-op. Soft-deleted teams are skipped: their history ends with their deletion.
        //
        // Each payload is written by hand, frozen at the shape its baseline type has when this ships, and has
        // to deserialize into that type exactly as the serializer would have written it — including the
        // computed properties the serializer emits (a date range's days, an iteration range's effective
        // bounds). BackfillBaselineActivityMigrationTests compares every aggregate's row against the entry
        // ActivityLogEntryFactory builds for the same event.
        //
        // EventId is BaselineEventId.For: a version 5 UUID of "{AggregateType}:{id}" in the baseline
        // namespace. SQL Server reads the first three groups of a uniqueidentifier's bytes little-endian, so
        // the big-endian hash is reordered before the cast.
        //
        // The status CASEs map stored enum names to their values; a name missing from one leaves that
        // record's payload null, and the migration stops rather than skip it.

        // SystemUser.Id, frozen here: a migration must keep writing what it wrote when it shipped.
        internal const string SystemUserId = "11111111-1111-1111-1111-111111111111";

        internal const string BaselineNamespace = "0x5B0E7C2A3F6D4C8E9A1B7D2F4E6A8C30";

        /// <summary>Whole days from NodaTime's Instant.MinValue to 0001-01-01T00:00:00Z.</summary>
        private const int DaysFromInstantMinValueToYearOne = 3652060;

        private static readonly string[] BaselineEventTypes =
        [
            "ProjectBaselinedEvent", "ProgramBaselinedEvent", "ProjectPortfolioBaselinedEvent",
            "StrategicInitiativeBaselinedEvent", "StrategicThemeBaselinedEvent", "IterationBaselinedEvent",
            "TeamBaselinedEvent", "WorkflowBaselinedEvent", "ProductBaselinedEvent",
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
                [EventVersion] varchar(16) NOT NULL,
                [DomainArea] varchar(64) NOT NULL,
                [Summary] nvarchar(512) NOT NULL,
                [Body] nvarchar(max) NULL,
                [RecordCreated] datetime2 NULL,
                [RecordCreatedBy] nvarchar(450) NULL,
                PRIMARY KEY ([AggregateType], [AggregateId])
            );

            -- Project
            INSERT INTO #Baseline
            SELECT 'Project', p.[Id], 'ProjectBaselinedEvent', '1.1', 'Ppm', N'Project Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("p.[Id]")}}
                    + N',"key":' + {{Str("p.[Key]")}}
                    + N',"name":' + {{Str("p.[Name]")}}
                    + N',"description":' + {{Str("p.[Description]")}}
                    + N',"expenditureCategoryId":' + {{Int("p.[ExpenditureCategoryId]")}}
                    + N',"statusId":' + {{Int(EnumValue("p.[Status]", ("Proposed", 1), ("Active", 2), ("Completed", 3), ("Canceled", 4), ("Approved", 5)))}}
                    + N',"dateRange":' + {{DateRange("p.[Start]", "p.[End]")}}
                    + N',"portfolioId":' + {{Guid("p.[PortfolioId]")}}
                    + N',"programId":' + {{Guid("p.[ProgramId]")}}
                    + N',"businessCase":' + {{Str("p.[BusinessCase]")}}
                    + N',"expectedBenefits":' + {{Str("p.[ExpectedBenefits]")}}
                    + N',"roles":' + {{Roles("[Ppm].[ProjectRoleAssignments]", "p.[Id]", ("Sponsor", 1), ("Owner", 2), ("Manager", 3), ("Member", 4))}}
                    + N',"strategicThemes":' + {{Themes("[Ppm].[ProjectStrategicThemes]", "p.[Id]")}},
                p.[SystemCreated], p.[SystemCreatedBy]
            FROM [Ppm].[Projects] p
            WHERE {{NotRecorded("Project", "p.[Id]", "ProjectCreatedEvent", "ProjectBaselinedEvent")}};

            -- Program
            INSERT INTO #Baseline
            SELECT 'Program', p.[Id], 'ProgramBaselinedEvent', '1.0', 'Ppm', N'Program Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("p.[Id]")}}
                    + N',"key":' + {{Int("p.[Key]")}}
                    + N',"name":' + {{Str("p.[Name]")}}
                    + N',"description":' + {{Str("p.[Description]")}}
                    + N',"statusId":' + {{Int(EnumValue("p.[Status]", ("Proposed", 1), ("Active", 2), ("Completed", 3), ("Canceled", 4)))}}
                    + N',"dateRange":' + {{DateRange("p.[Start]", "p.[End]")}}
                    + N',"portfolioId":' + {{Guid("p.[PortfolioId]")}}
                    + N',"roles":' + {{Roles("[Ppm].[ProgramRoleAssignments]", "p.[Id]", ("Sponsor", 1), ("Owner", 2), ("Manager", 3))}}
                    + N',"strategicThemes":' + {{Themes("[Ppm].[ProgramStrategicThemes]", "p.[Id]")}},
                p.[SystemCreated], p.[SystemCreatedBy]
            FROM [Ppm].[Programs] p
            WHERE {{NotRecorded("Program", "p.[Id]", "ProgramCreatedEvent", "ProgramBaselinedEvent")}};

            -- Project portfolio
            INSERT INTO #Baseline
            SELECT 'ProjectPortfolio', p.[Id], 'ProjectPortfolioBaselinedEvent', '1.0', 'Ppm', N'Project Portfolio Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("p.[Id]")}}
                    + N',"key":' + {{Int("p.[Key]")}}
                    + N',"name":' + {{Str("p.[Name]")}}
                    + N',"description":' + {{Str("p.[Description]")}}
                    + N',"statusId":' + {{Int(EnumValue("p.[Status]", ("Proposed", 1), ("Active", 2), ("OnHold", 3), ("Closed", 4), ("Archived", 5)))}}
                    + N',"roles":' + {{Roles("[Ppm].[PortfolioRoleAssignments]", "p.[Id]", ("Sponsor", 1), ("Owner", 2), ("Manager", 3))}},
                p.[SystemCreated], p.[SystemCreatedBy]
            FROM [Ppm].[Portfolios] p
            WHERE {{NotRecorded("ProjectPortfolio", "p.[Id]", "ProjectPortfolioCreatedEvent", "ProjectPortfolioBaselinedEvent")}};

            -- Strategic initiative
            INSERT INTO #Baseline
            SELECT 'StrategicInitiative', s.[Id], 'StrategicInitiativeBaselinedEvent', '1.1', 'Ppm', N'Strategic Initiative Baselined',
                CAST(N'{"portfolioId":' AS nvarchar(max)) + {{Guid("s.[PortfolioId]")}}
                    + N',"strategicInitiativeId":' + {{Guid("s.[Id]")}}
                    + N',"key":' + {{Int("s.[Key]")}}
                    + N',"name":' + {{Str("s.[Name]")}}
                    + N',"description":' + {{Str("s.[Description]")}}
                    + N',"status":' + {{Int(EnumValue("s.[Status]", ("Proposed", 1), ("Approved", 2), ("Active", 3), ("OnHold", 4), ("Completed", 5), ("Canceled", 6)))}}
                    + N',"dateRange":' + {{DateRange("s.[Start]", "s.[End]")}}
                    + N',"roles":' + {{Roles("[Ppm].[StrategicInitiativeRoleAssignments]", "s.[Id]", ("Sponsor", 1), ("Owner", 2))}},
                s.[SystemCreated], s.[SystemCreatedBy]
            FROM [Ppm].[StrategicInitiatives] s
            WHERE {{NotRecorded("StrategicInitiative", "s.[Id]", "StrategicInitiativeCreatedEvent", "StrategicInitiativeBaselinedEvent")}};

            -- Strategic theme
            INSERT INTO #Baseline
            SELECT 'StrategicTheme', t.[Id], 'StrategicThemeBaselinedEvent', '1.0', 'StrategicManagement', N'Strategic Theme Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("t.[Id]")}}
                    + N',"key":' + {{Int("t.[Key]")}}
                    + N',"name":' + {{Str("t.[Name]")}}
                    + N',"description":' + {{Str("t.[Description]")}}
                    + N',"state":' + {{EnumName("t.[State]")}},
                t.[SystemCreated], t.[SystemCreatedBy]
            FROM [StrategicManagement].[StrategicThemes] t
            WHERE {{NotRecorded("StrategicTheme", "t.[Id]", "StrategicThemeCreatedEvent", "StrategicThemeBaselinedEvent")}};

            -- Iteration
            INSERT INTO #Baseline
            SELECT 'Iteration', i.[Id], 'IterationBaselinedEvent', '1.0', 'Planning', N'Iteration Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("i.[Id]")}}
                    + N',"key":' + {{Int("i.[Key]")}}
                    + N',"name":' + {{Str("i.[Name]")}}
                    + N',"type":' + {{EnumName("i.[Type]")}}
                    + N',"state":' + {{EnumName("i.[State]")}}
                    + N',"dateRange":' + {{IterationDateRange("i.[Start]", "i.[End]")}}
                    + N',"teamId":' + {{Guid("i.[TeamId]")}},
                i.[SystemCreated], i.[SystemCreatedBy]
            FROM [Planning].[Iterations] i
            WHERE {{NotRecorded("Iteration", "i.[Id]", "IterationCreatedEvent", "IterationBaselinedEvent")}};

            -- Team and team of teams
            INSERT INTO #Baseline
            SELECT 'Team', t.[Id], 'TeamBaselinedEvent', '1.0', 'Organization', N'Team Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("t.[Id]")}}
                    + N',"key":' + {{Int("t.[Key]")}}
                    + N',"code":' + {{Str("t.[Code]")}}
                    + N',"name":' + {{Str("t.[Name]")}}
                    + N',"description":' + {{Str("t.[Description]")}}
                    + N',"type":' + {{EnumName("t.[Type]")}}
                    + N',"activeDate":' + {{Date("t.[ActiveDate]")}}
                    + N',"inactiveDate":' + {{Date("t.[InactiveDate]")}}
                    + N',"isActive":' + {{Bool("t.[IsActive]")}},
                t.[SystemCreated], t.[SystemCreatedBy]
            FROM [Organization].[Teams] t
            WHERE t.[IsDeleted] = 0
              AND {{NotRecorded("Team", "t.[Id]", "TeamCreatedEvent", "TeamBaselinedEvent")}};

            -- Status workflow
            INSERT INTO #Baseline
            SELECT 'Workflow', w.[Id], 'WorkflowBaselinedEvent', '1.0', 'Work', N'Workflow Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("w.[Id]")}}
                    + N',"key":' + {{Int("w.[Key]")}}
                    + N',"name":' + {{Str("w.[Name]")}}
                    + N',"description":' + {{Str("w.[Description]")}}
                    + N',"ownerType":' + {{Str("w.[OwnerType]")}}
                    + N',"isSystem":' + {{Bool("w.[IsSystem]")}}
                    + N',"sourceWorkflowId":null'
                    + N',"statuses":[' + COALESCE((
                        SELECT STRING_AGG(
                            CAST(N'{"statusId":' AS nvarchar(max)) + {{Guid("ws.[Id]")}}
                                + N',"name":' + {{Str("ws.[Name]")}}
                                + N',"description":' + {{Str("ws.[Description]")}}
                                + N',"category":' + {{EnumName("ws.[Category]")}}
                                + N',"alias":' + {{Int("ws.[Alias]")}}
                                + N',"order":' + {{Int("ws.[Order]")}}
                                + N'}',
                            N',') WITHIN GROUP (ORDER BY ws.[Order])
                        FROM [StatusWorkflows].[WorkflowStatuses] ws
                        WHERE ws.[WorkflowId] = w.[Id]), N'') + N']',
                w.[SystemCreated], w.[SystemCreatedBy]
            FROM [StatusWorkflows].[StatusWorkflows] w
            WHERE {{NotRecorded("Workflow", "w.[Id]", "WorkflowCreatedEvent", "WorkflowBaselinedEvent")}};

            -- Product
            INSERT INTO #Baseline
            SELECT 'Product', p.[Id], 'ProductBaselinedEvent', '1.0', 'ProductManagement', N'Product Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("p.[Id]")}}
                    + N',"key":' + {{Int("p.[Key]")}}
                    + N',"name":' + {{Str("p.[Name]")}}
                    + N',"description":' + {{Str("p.[Description]")}}
                    + N',"productTypeId":' + {{Guid("p.[ProductTypeId]")}}
                    + N',"parentId":' + {{Guid("p.[ParentId]")}}
                    + N',"statusId":' + {{Guid("p.[StatusId]")}}
                    + N',"statusCategory":' + {{EnumName("p.[StatusCategory]")}},
                p.[SystemCreated], p.[SystemCreatedBy]
            FROM [ProductManagement].[Products] p
            WHERE {{NotRecorded("Product", "p.[Id]", "ProductAddedEvent", "ProductBaselinedEvent")}};

            DECLARE @Unmapped int = (SELECT COUNT(*) FROM #Baseline WHERE [Body] IS NULL);
            IF @Unmapped > 0
            BEGIN
                DECLARE @Message nvarchar(200) = CONCAT(
                    'Backfill-Baseline-Activity could not build ', @Unmapped,
                    ' baseline payload(s). A stored status is missing from its CASE.');
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
                b.[EventVersion],
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
                    -- A SystemCreated older than any real record is the column's default, not a creation date.
                    + N',"recordCreatedOn":' + CASE WHEN b.[RecordCreated] IS NULL OR b.[RecordCreated] < '1900-01-01'
                        THEN N'null' ELSE N'"' + {{Iso("b.[RecordCreated]")}} + N'"' END
                    + N',"recordCreatedById":' + {{Guid("u.[EmployeeId]")}}
                    + N',"timestamp":"' + {{Iso("@Now")}} + N'"'
                    + N',"eventId":"' + LOWER(CONVERT(nvarchar(36), id.[EventId])) + N'"'
                    + N',"actor":{"kind":"system","userId":"{{SystemUserId}}","employeeId":null}'
                    + N',"eventVersion":"' + b.[EventVersion] + N'"}',
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

        private static string Int(string expression) => $"CONVERT(nvarchar(11), {expression})";

        private static string Bool(string column) => $"CASE WHEN {column} = 1 THEN N'true' ELSE N'false' END";

        private static string Date(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + CONVERT(nchar(10), {column}, 23) + N'\"' END";

        /// <summary>An enum stored by name, as JsonStringEnumConverter writes it: camelCased.</summary>
        private static string EnumName(string column) =>
            $"N'\"' + LOWER(LEFT({column}, 1)) + SUBSTRING({column}, 2, 64) + N'\"'";

        /// <summary>An enum stored by name, as a payload that carries its numeric value.</summary>
        private static string EnumValue(string column, params (string Name, int Value)[] members) =>
            $"CASE {column} {string.Join(" ", members.Select(m => $"WHEN '{m.Name}' THEN {m.Value}"))} END";

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

        /// <summary>A LocalDateRange, null when either end is unset.</summary>
        private static string DateRange(string start, string end) => $"""
            CASE WHEN {start} IS NULL OR {end} IS NULL THEN N'null'
                 ELSE N'{"{"}"start":"' + CONVERT(nchar(10), {start}, 23) + N'","end":"' + CONVERT(nchar(10), {end}, 23)
                     + N'","days":' + CONVERT(nvarchar(11), DATEDIFF(DAY, {start}, {end}) + 1) + N'{"}"}' END
            """;

        /// <summary>
        /// An IterationDateRange, whose unset ends read as Instant.MinValue and Instant.MaxValue. Days is the whole
        /// days of the effective span, plus one.
        /// </summary>
        private static string IterationDateRange(string start, string end) => $"""
            (N'{"{"}"start":' + CASE WHEN {start} IS NULL THEN N'null' ELSE N'"' + {Iso(start)} + N'"' END
                + N',"end":' + CASE WHEN {end} IS NULL THEN N'null' ELSE N'"' + {Iso(end)} + N'"' END
                + N',"effectiveStart":"' + CASE WHEN {start} IS NULL THEN N'-9998-01-01T00:00:00Z' ELSE {Iso(start)} END
                + N'","effectiveEnd":"' + CASE WHEN {end} IS NULL THEN N'9999-12-31T23:59:59.999999999Z' ELSE {Iso(end)} END
                + N'","days":' + CONVERT(nvarchar(11),
                    CASE WHEN {start} IS NULL AND {end} IS NULL THEN 7304119
                         WHEN {start} IS NULL THEN {DaysFromInstantMinValueToYearOne} + DATEDIFF(DAY, '0001-01-01', CAST({end} AS date)) + 1
                         WHEN {end} IS NULL THEN DATEDIFF(DAY, CAST({start} AS date), '9999-12-31') + 1
                         ELSE CAST(DATEDIFF_BIG(NANOSECOND, {start}, {end}) / 86400000000000 AS int) + 1
                    END)
                + N'{"}"}')
            """;

        /// <summary>A RoleManager.ToRoleMap: each role's value mapped to the ids of the employees holding it.</summary>
        private static string Roles(string table, string ownerId, params (string Name, int Value)[] roles) => $"""
            COALESCE((
                SELECT N'{"{"}' + STRING_AGG(g.[Json], N',') WITHIN GROUP (ORDER BY g.[RoleValue]) + N'{"}"}'
                FROM
                (
                    SELECT {EnumValue("ra.[Role]", roles)} AS [RoleValue],
                        CAST(N'"' + CONVERT(nvarchar(11), {EnumValue("ra.[Role]", roles)}) + N'":[' AS nvarchar(max))
                            + STRING_AGG(CAST(N'"' + LOWER(CONVERT(nvarchar(36), ra.[EmployeeId])) + N'"' AS nvarchar(max)), N',')
                                WITHIN GROUP (ORDER BY ra.[EmployeeId])
                            + N']' AS [Json]
                    FROM {table} ra
                    WHERE ra.[ObjectId] = {ownerId}
                    GROUP BY ra.[Role]
                ) g
            ), N'{"{"}{"}"}')
            """;

        private static string Themes(string table, string ownerId) => $"""
            (N'[' + COALESCE((
                SELECT STRING_AGG(CAST(N'"' + LOWER(CONVERT(nvarchar(36), st.[StrategicThemeId])) + N'"' AS nvarchar(max)), N',')
                    WITHIN GROUP (ORDER BY st.[StrategicThemeId])
                FROM {table} st
                WHERE st.[ObjectId] = {ownerId}
            ), N'') + N']')
            """;
    }
}
