using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class BaselineEmployeeOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BaselineOwnershipSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [App].[ActivityLogs]
                WHERE [EventType] = 'EmployeeAdoptedEvent' AND [ActorKind] = 'System' AND [AggregateType] = 'Employee';

                UPDATE [Organization].[Employees]
                SET [Ownership] = 'Owned', [Connector] = NULL, [SystemId] = NULL, [ExternalId] = NULL;
                """);
        }

        // SystemUser.Id, frozen here: a migration must keep writing what it wrote when it shipped.
        internal const string SystemUserId = "11111111-1111-1111-1111-111111111111";

        // Before ownership existed the active people connection wrote every employee, so where one is active each
        // employee becomes managed by its system, with the employee number as its external id. Where none is
        // active, or more than one (which a people sync refuses to run with), every employee stays owned.
        // Soft-deleted employees stay owned: nothing syncs them. A number longer than the external id column
        // cannot be a reference, so that employee stays owned too.
        //
        // Each employee it hands to the sync is recorded as the EmployeeAdoptedEvent a live Adopt raises, so its
        // log shows when its ownership began. The payload is written by hand, frozen at the 1.0 shape, and has to
        // read back exactly as the serializer writes it; EmployeeOwnershipBaselineTests compares it with
        // ActivityLogEntryFactory's. Only owned employees with no reference are touched, so a second run writes
        // nothing.
        internal static string BaselineOwnershipSql => $$"""
            DECLARE @Now datetime2(7) = SYSUTCDATETIME();
            DECLARE @Connector varchar(32);
            DECLARE @SystemId varchar(64);

            IF (SELECT COUNT(*) FROM [AppIntegrations].[Connections]
                WHERE [Connector] IN ('Entra', 'Workday') AND [IsActive] = 1 AND [IsDeleted] = 0) = 1
            BEGIN
                SELECT
                    @Connector = [Connector],
                    @SystemId = LEFT(CASE [Connector]
                        WHEN 'Entra' THEN JSON_VALUE([Configuration], '$.TenantId')
                        ELSE JSON_VALUE([Configuration], '$.TenantAlias')
                    END, 64)
                FROM [AppIntegrations].[Connections]
                WHERE [Connector] IN ('Entra', 'Workday') AND [IsActive] = 1 AND [IsDeleted] = 0;
            END

            IF @Connector IS NOT NULL
            BEGIN
                DECLARE @Adopted TABLE (
                    [Id] uniqueidentifier NOT NULL,
                    [PreviousAccessEndDate] date NULL,
                    [EventId] uniqueidentifier NOT NULL DEFAULT NEWID());

                UPDATE [Organization].[Employees]
                SET [Ownership] = 'Managed',
                    [Connector] = @Connector,
                    [SystemId] = NULLIF(LTRIM(RTRIM(@SystemId)), ''),
                    [ExternalId] = [EmployeeNumber],
                    [AccessEndDate] = NULL
                OUTPUT inserted.[Id], deleted.[AccessEndDate] INTO @Adopted ([Id], [PreviousAccessEndDate])
                WHERE [IsDeleted] = 0
                  AND [Ownership] = 'Owned'
                  AND [Connector] IS NULL
                  AND LEN([EmployeeNumber]) <= 64;

                INSERT INTO [App].[ActivityLogs]
                (
                    [EventId], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                    [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary]
                )
                SELECT
                    a.[EventId],
                    'EmployeeAdoptedEvent',
                    'Updated',
                    '1.0',
                    'Organization',
                    'Employee',
                    a.[Id],
                    'System',
                    '{{SystemUserId}}',
                    NULL,
                    @Now,
                    0,
                    NULL,
                    CAST(N'{"id":"' AS nvarchar(max)) + LOWER(CONVERT(nvarchar(36), a.[Id]))
                        + N'","previousOwnership":"owned","previousConnector":null,"previousSystemId":null'
                        + N',"previousAccessEndDate":'
                            + CASE WHEN a.[PreviousAccessEndDate] IS NULL THEN N'null'
                                   ELSE N'"' + CONVERT(nchar(10), a.[PreviousAccessEndDate], 23) + N'"' END
                        + N',"ownership":"managed"'
                        + N',"connector":"' + CASE @Connector WHEN 'Entra' THEN N'entra' ELSE N'workday' END + N'"'
                        + N',"systemId":'
                            + CASE WHEN e.[SystemId] IS NULL THEN N'null'
                                   ELSE N'"' + STRING_ESCAPE(e.[SystemId], 'json') + N'"' END
                        + N',"timestamp":"' + {{Iso("@Now")}} + N'"'
                        + N',"eventId":"' + LOWER(CONVERT(nvarchar(36), a.[EventId])) + N'"'
                        + N',"actor":{"kind":"system","userId":"{{SystemUserId}}","employeeId":null}'
                        + N',"eventVersion":"1.0"}',
                    N'Employee Adopted'
                FROM @Adopted a
                INNER JOIN [Organization].[Employees] e ON e.[Id] = a.[Id];
            END
            """;

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
