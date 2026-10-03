using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillEmployeeAndIdentityMappingBaselineActivity : Migration
    {
        // Gives every employee and external identity mapping that has no creation entry a baseline, as
        // Backfill-Security-Configuration-Baseline-Activity did for tokens, providers and connections, and removes
        // whatever entries such a record already had. A record with a creation or baseline entry is left as it is,
        // which is also what makes a second run a no-op. Soft-deleted employees are skipped: they cannot change again.
        //
        // Each payload is written by hand, frozen at the shape its baseline type has when this ships, and has to
        // deserialize into that type exactly as the serializer would have written it. Enums are stored by name and
        // mapped to the camel-case names the events carry.
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
                  AND [EventType] IN ('EmployeeBaselinedEvent', 'ExternalIdentityMappingBaselinedEvent');
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
                [RecordCreated] datetime2 NULL,
                [RecordCreatedBy] nvarchar(450) NULL,
                PRIMARY KEY ([AggregateType], [AggregateId])
            );

            INSERT INTO #Baseline
            SELECT 'Employee', e.[Id], 'EmployeeBaselinedEvent', 'Organization', N'Employee Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("e.[Id]")}}
                    + N',"key":' + {{Int("e.[Key]")}}
                    + N',"managerId":' + {{Guid("e.[ManagerId]")}}
                    + N',"isActive":' + {{Bool("e.[IsActive]")}},
                e.[SystemCreated], e.[SystemCreatedBy]
            FROM [Organization].[Employees] e
            WHERE e.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateId] = e.[Id] AND a.[AggregateType] = 'Employee'
                                AND a.[EventType] IN ('EmployeeCreatedEvent', 'EmployeeBaselinedEvent'));

            INSERT INTO #Baseline
            SELECT 'ExternalIdentityMapping', m.[Id], 'ExternalIdentityMappingBaselinedEvent', 'AppIntegration', N'External Identity Mapping Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("m.[Id]")}}
                    + N',"connector":' + CASE m.[Connector]
                        WHEN 'AzureDevOps' THEN N'"azureDevOps"'
                        WHEN 'AzureOpenAI' THEN N'"azureOpenAI"'
                        WHEN 'Entra' THEN N'"entra"'
                        WHEN 'Workday' THEN N'"workday"'
                      END
                    + N',"connectionId":' + {{Guid("m.[ConnectionId]")}}
                    + N',"externalId":' + {{Str("m.[ExternalId]")}}
                    + N',"employeeId":' + {{Guid("m.[EmployeeId]")}}
                    + N',"status":' + CASE m.[Status]
                        WHEN 'Unmapped' THEN N'"unmapped"'
                        WHEN 'AutoMatched' THEN N'"autoMatched"'
                        WHEN 'ManuallyMapped' THEN N'"manuallyMapped"'
                        WHEN 'Ignored' THEN N'"ignored"'
                      END,
                m.[SystemCreated], m.[SystemCreatedBy]
            FROM [AppIntegrations].[ExternalIdentityMappings] m
            WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateId] = m.[Id] AND a.[AggregateType] = 'ExternalIdentityMapping'
                                AND a.[EventType] IN ('ExternalIdentityMappingCreatedEvent', 'ExternalIdentityMappingBaselinedEvent'));

            DECLARE @Unbuilt int = (SELECT COUNT(*) FROM #Baseline WHERE [Body] IS NULL);
            IF @Unbuilt > 0
            BEGIN
                DECLARE @Message nvarchar(200) = CONCAT(
                    'Backfill-Employee-And-Identity-Mapping-Baseline-Activity could not build ', @Unbuilt, ' baseline payload(s).');
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

        private static string Str(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + STRING_ESCAPE({column}, 'json') + N'\"' END";

        private static string Guid(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE N'\"' + LOWER(CONVERT(nvarchar(36), {column})) + N'\"' END";

        private static string Int(string column) =>
            $"CASE WHEN {column} IS NULL THEN N'null' ELSE CONVERT(nvarchar(11), {column}) END";

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
