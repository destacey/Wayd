using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSecurityConfigurationBaselineActivity : Migration
    {
        // Gives every personal access token, OIDC provider and connection that has no creation entry a baseline, as
        // Backfill-Scoring-Model-Baseline-Activity did for scoring models, and removes whatever entries such a record
        // already had. A record with a creation or baseline entry is left as it is, which is also what makes a second
        // run a no-op. Revoked tokens and soft-deleted connections are skipped: neither can change again, and a
        // token's creation payload has no way to say it was revoked. A token's baseline is also filed under its owner,
        // as PersonalAccessTokenBaselinedEvent.RelatedAggregates files it, or the user's Activity would never list it.
        //
        // Each payload is written by hand, frozen at the shape its baseline type has when this ships, and has to
        // deserialize into that type exactly as the serializer would have written it. A connection's settings are read
        // out of its Configuration JSON, which the default serializer wrote: enums are numbers there and are mapped to
        // the names the events carry, and a property an older configuration lacks reads as its type's default, as it
        // does when the configuration is loaded.
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
                  AND a.[EventType] = 'PersonalAccessTokenBaselinedEvent';

                DELETE FROM [App].[ActivityLogs]
                WHERE [Category] = 'Baseline'
                  AND [EventType] IN ('PersonalAccessTokenBaselinedEvent', 'OidcProviderBaselinedEvent', 'ConnectionBaselinedEvent');
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
            SELECT 'PersonalAccessToken', t.[Id], 'PersonalAccessTokenBaselinedEvent', 'Identity', N'Personal Access Token Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("t.[Id]")}}
                    + N',"userId":' + {{Str("t.[UserId]")}}
                    + N',"name":' + {{Str("t.[Name]")}}
                    + N',"expiresAt":"' + {{Iso("t.[ExpiresAt]")}} + N'"'
                    + N',"scopes":' + {{Str("t.[Scopes]")}},
                t.[SystemCreated], t.[SystemCreatedBy]
            FROM [Identity].[PersonalAccessTokens] t
            WHERE t.[RevokedAt] IS NULL
              AND NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateId] = t.[Id] AND a.[AggregateType] = 'PersonalAccessToken'
                                AND a.[EventType] IN ('PersonalAccessTokenCreatedEvent', 'PersonalAccessTokenBaselinedEvent'));

            INSERT INTO #Baseline
            SELECT 'OidcProvider', p.[Id], 'OidcProviderBaselinedEvent', 'Identity', N'Oidc Provider Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("p.[Id]")}}
                    + N',"name":' + {{Str("p.[Name]")}}
                    + N',"label":' + {{Str("p.[DisplayName]")}}
                    + N',"providerType":' + CASE p.[ProviderType]
                        WHEN 'MicrosoftEntraId' THEN N'"microsoftEntraId"'
                        WHEN 'GenericOidc' THEN N'"genericOidc"'
                      END
                    + N',"configuration":{"authority":' + {{Str("p.[Authority]")}}
                        + N',"clientId":' + {{Str("p.[ClientId]")}}
                        + N',"audience":' + {{Str("p.[Audience]")}}
                        + N',"scopes":' + COALESCE(NULLIF(p.[Scopes], N''), N'[]')
                        + N',"allowedTenantIds":' + COALESCE(NULLIF(p.[AllowedTenantIds], N''), N'null')
                        + N',"clockSkewSeconds":' + {{Int("p.[ClockSkewSeconds]")}}
                        + N'}'
                    + N',"registrationPolicy":{"allowAutoRegistration":' + {{Bool("p.[AllowAutoRegistration]")}}
                        + N',"requireEmployeeRecord":' + CASE WHEN p.[RequireEmployeeRecord] IS NULL THEN N'null' ELSE {{Bool("p.[RequireEmployeeRecord]")}} END
                        + N',"defaultRoleId":' + {{Str("p.[DefaultRoleId]")}}
                        + N'}'
                    + N',"isEnabled":' + {{Bool("p.[IsEnabled]")}},
                p.[SystemCreated], p.[SystemCreatedBy]
            FROM [Identity].[OidcProviders] p
            WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateId] = p.[Id] AND a.[AggregateType] = 'OidcProvider'
                                AND a.[EventType] IN ('OidcProviderCreatedEvent', 'OidcProviderBaselinedEvent'));

            INSERT INTO #Baseline
            SELECT 'Connection', c.[Id], 'ConnectionBaselinedEvent', 'AppIntegration', N'Connection Baselined',
                CAST(N'{"id":' AS nvarchar(max)) + {{Guid("c.[Id]")}}
                    + N',"name":' + {{Str("c.[Name]")}}
                    + N',"description":' + {{Str("c.[Description]")}}
                    + N',"connector":' + CASE c.[Connector]
                        WHEN 'AzureDevOps' THEN N'"azureDevOps"'
                        WHEN 'AzureOpenAI' THEN N'"azureOpenAI"'
                        WHEN 'Entra' THEN N'"entra"'
                        WHEN 'Workday' THEN N'"workday"'
                      END
                    + N',"isActive":' + {{Bool("c.[IsActive]")}}
                    + N',"settings":[' + CASE c.[Connector]
                        WHEN 'AzureDevOps' THEN
                            {{Setting("Organization", JsonString("Organization"))}}
                        WHEN 'AzureOpenAI' THEN
                            {{Setting("BaseUrl", JsonString("BaseUrl"))}}
                            + N',' + {{Setting("DeploymentName", JsonString("DeploymentName"))}}
                            + N',' + {{Setting("DefaultTemperature", JsonNumber("DefaultTemperature"))}}
                            + N',' + {{Setting("DefaultMaxOutputTokens", JsonNumber("DefaultMaxOutputTokens"))}}
                            + N',' + {{Setting("JsonModePreferred", JsonBool("JsonModePreferred"))}}
                        WHEN 'Entra' THEN
                            {{Setting("TenantId", JsonString("TenantId"))}}
                            + N',' + {{Setting("ClientId", JsonString("ClientId"))}}
                            + N',' + {{Setting("AllUsersGroupObjectId", JsonString("AllUsersGroupObjectId"))}}
                            + N',' + {{Setting("IncludeDisabledUsers", JsonBool("IncludeDisabledUsers"))}}
                            + N',' + {{Setting("MatchBy", MatchBy)}}
                            + N',' + {{Setting("NormalizeNameCasing", JsonBool("NormalizeNameCasing"))}}
                        WHEN 'Workday' THEN
                            {{Setting("WsdlUrl", JsonString("WsdlUrl"))}}
                            + N',' + {{Setting("IsuUsername", JsonString("IsuUsername"))}}
                            + N',' + {{Setting("WorkerKey", WorkerKey)}}
                            + N',' + {{Setting("IncludeInactive", JsonBool("IncludeInactive"))}}
                            + N',' + {{Setting("MatchBy", MatchBy)}}
                            + N',' + {{Setting("UseUserIdAsEmailFallback", JsonBool("UseUserIdAsEmailFallback"))}}
                            + N',' + {{Setting("UsePreferredName", JsonBool("UsePreferredName"))}}
                            + N',' + {{Setting("NormalizeNameCasing", JsonBool("NormalizeNameCasing"))}}
                            + N',' + {{Setting("DepartmentOrganizationTypeId", JsonString("DepartmentOrganizationTypeId"))}}
                            + N',' + {{Setting("OrgExclusions", "x.[OrgExclusions]")}}
                      END + N']',
                c.[SystemCreated], c.[SystemCreatedBy]
            FROM [AppIntegrations].[Connections] c
            -- Exclusions as "type:reference", ordered ordinally and comma-joined, as WorkdayConnection describes them.
            CROSS APPLY
            (
                SELECT COALESCE((
                    SELECT STRING_AGG(CAST(e.[TypeId] + N':' + e.[Reference] AS nvarchar(max)), N', ')
                        WITHIN GROUP (ORDER BY (e.[TypeId] + N':' + e.[Reference]) COLLATE Latin1_General_BIN2)
                    FROM OPENJSON(c.[Configuration], '$.OrgExclusions')
                        WITH ([TypeId] nvarchar(400) '$.OrganizationTypeId', [Reference] nvarchar(400) '$.OrganizationReference') e
                    WHERE c.[Connector] = 'Workday'), N'') AS [OrgExclusions]
            ) x
            WHERE c.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateId] = c.[Id] AND a.[AggregateType] = 'Connection'
                                AND a.[EventType] IN ('ConnectionCreatedEvent', 'ConnectionBaselinedEvent'));

            DECLARE @Unbuilt int = (SELECT COUNT(*) FROM #Baseline WHERE [Body] IS NULL);
            IF @Unbuilt > 0
            BEGIN
                DECLARE @Message nvarchar(200) = CONCAT(
                    'Backfill-Security-Configuration-Baseline-Activity could not build ', @Unbuilt, ' baseline payload(s).');
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

            INSERT INTO [App].[ActivityLogRelatedAggregates] ([ActivityLogId], [AggregateType], [AggregateId])
            SELECT a.[Id], 'ApplicationUser', CONVERT(uniqueidentifier, JSON_VALUE(a.[Payload], '$.userId'))
            FROM [App].[ActivityLogs] a
            INNER JOIN #Baseline b
                ON b.[AggregateType] = a.[AggregateType]
                AND b.[AggregateId] = a.[AggregateId]
            WHERE a.[EventType] = 'PersonalAccessTokenBaselinedEvent';

            DROP TABLE #Baseline;
            """;

        // EmployeeMatchProperty and WorkdayWorkerKey by value, as they are when this ships. An unknown value reads as
        // its number, as Enum.ToString does.
        private static readonly string MatchBy = EnumName("MatchBy", ("0", "Email"), ("1", "EmployeeNumber"));

        private static readonly string WorkerKey = EnumName("WorkerKey", ("0", "Wid"), ("1", "EmployeeId"));

        private static string Setting(string name, string valueExpression) =>
            $"(N'{{\"name\":\"{name}\",\"value\":' + {Str(valueExpression)} + N'}}')";

        private static string JsonString(string property) => $"JSON_VALUE(c.[Configuration], '$.{property}')";

        private static string JsonNumber(string property) => $"COALESCE(JSON_VALUE(c.[Configuration], '$.{property}'), N'0')";

        private static string JsonBool(string property) => $"COALESCE(JSON_VALUE(c.[Configuration], '$.{property}'), N'false')";

        private static string EnumName(string property, params (string Value, string Name)[] members)
        {
            var value = JsonNumber(property);
            var cases = string.Concat(members.Select(m => $" WHEN N'{m.Value}' THEN N'{m.Name}'"));
            return $"(CASE {value}{cases} ELSE {value} END)";
        }

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
