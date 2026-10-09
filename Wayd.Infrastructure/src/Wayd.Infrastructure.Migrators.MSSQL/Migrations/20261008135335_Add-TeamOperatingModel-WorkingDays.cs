using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamOperatingModelWorkingDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing model worked Monday to Friday, which is what WorkingWeek.Parse reads back.
            migrationBuilder.AddColumn<string>(
                name: "WorkingDays",
                schema: "Organization",
                table: "TeamOperatingModels",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Monday,Tuesday,Wednesday,Thursday,Friday");

            migrationBuilder.Sql(RecordAssignedWorkingDaysSql);
        }

        // SystemUser.Id, frozen here: a migration must keep writing what it wrote when it shipped.
        internal const string SystemUserId = "11111111-1111-1111-1111-111111111111";

        // Records the working days each existing model was just given as the TeamOperatingModelCorrectedEvent a
        // System correction raises: previous working days null, because Wayd did not track them, and the new ones
        // from the column. A team's log then shows when its working week began to be recorded. The payload is
        // written by hand, frozen at the 1.1 shape, and has to read back exactly as the serializer would have
        // written it, including FlexibleDateRange's computed EffectiveEnd and Days.
        // TeamOperatingModelWorkingDaysBackfillTests compares it with ActivityLogEntryFactory's.
        //
        // The EventId is derived from the model's id, so the entry cannot be written twice. Soft-deleted teams are
        // skipped, as the baseline backfill skipped them: their history ends with their deletion.
        internal static string RecordAssignedWorkingDaysSql => $$"""
            DECLARE @Now datetime2(7) = SYSUTCDATETIME();

            WITH Assigned AS
            (
                SELECT
                    {{EventIdOf("m.[Id]")}} AS [EventId],
                    m.[Start], m.[End], m.[Methodology], m.[SizingMethod], m.[TimeZone], m.[CommitmentGraceDays],
                    m.[WorkingDays], t.[Id] AS [TeamId], t.[Key] AS [TeamKey]
                FROM [Organization].[TeamOperatingModels] m
                INNER JOIN [Organization].[Teams] t ON t.[Id] = m.[TeamId]
                WHERE t.[IsDeleted] = 0
            )
            INSERT INTO [App].[ActivityLogs]
            (
                [EventId], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary]
            )
            SELECT
                a.[EventId],
                'TeamOperatingModelCorrectedEvent',
                'Updated',
                '1.1',
                'Organization',
                'Team',
                a.[TeamId],
                'System',
                '{{SystemUserId}}',
                NULL,
                @Now,
                0,
                NULL,
                CAST(N'{"id":"' AS nvarchar(max)) + LOWER(CONVERT(nvarchar(36), a.[TeamId]))
                    + N'","key":' + CONVERT(nvarchar(11), a.[TeamKey])
                    + N',"period":{"start":"' + CONVERT(nchar(10), a.[Start], 23)
                        + N'","end":' + COALESCE(N'"' + CONVERT(nchar(10), a.[End], 23) + N'"', N'null')
                        + N',"effectiveEnd":"' + CONVERT(nchar(10), COALESCE(a.[End], '9999-12-31'), 23)
                        + N'","days":' + CONVERT(nvarchar(11), DATEDIFF(DAY, a.[Start], COALESCE(a.[End], '9999-12-31')) + 1) + N'}'
                    + N',"settings":' + {{Settings("a", "N'[\"' + REPLACE(LOWER(a.[WorkingDays]), ',', '\",\"') + N'\"]'")}}
                    + N',"previous":' + {{Settings("a", "N'null'")}}
                    + N',"timestamp":"' + {{Iso("@Now")}} + N'"'
                    + N',"eventId":"' + LOWER(CONVERT(nvarchar(36), a.[EventId])) + N'"'
                    + N',"actor":{"kind":"system","userId":"{{SystemUserId}}","employeeId":null}'
                    + N',"eventVersion":"1.1"}',
                N'Team Operating Model Corrected'
            FROM Assigned a
            WHERE NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] l WHERE l.[EventId] = a.[EventId]);
            """;

        /// <summary>
        /// The id of the entry recording a model's assigned working days: the first 16 bytes of a SHA-256 of a
        /// fixed prefix and the model's id, so it is the same on every run and distinct from the model's own id.
        /// </summary>
        internal static string EventIdOf(string modelId) =>
            $"CONVERT(uniqueidentifier, CONVERT(binary(16), HASHBYTES('SHA2_256', N'TeamOperatingModelWorkingDays:' + LOWER(CONVERT(nvarchar(36), {modelId})))))";

        /// <summary>TeamOperatingModelSettings as JSON, its enums in camelCase as the activity serializer writes them.</summary>
        private static string Settings(string alias, string workingDaysJson) => $$"""
            (N'{"methodology":"' + {{CamelCase($"{alias}.[Methodology]")}}
                + N'","sizingMethod":"' + {{CamelCase($"{alias}.[SizingMethod]")}}
                + N'","timeZone":"' + STRING_ESCAPE({{alias}}.[TimeZone], 'json')
                + N'","commitmentGraceDays":' + CONVERT(nvarchar(11), {{alias}}.[CommitmentGraceDays])
                + N',"workingDays":' + {{workingDaysJson}} + N'}')
            """;

        private static string CamelCase(string column) => $"LOWER(LEFT({column}, 1)) + SUBSTRING({column}, 2, 64)";

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE l
                FROM [App].[ActivityLogs] l
                INNER JOIN [Organization].[TeamOperatingModels] m ON {EventIdOf("m.[Id]")} = l.[EventId]
                WHERE l.[EventType] = 'TeamOperatingModelCorrectedEvent';
                """);

            migrationBuilder.DropColumn(
                name: "WorkingDays",
                schema: "Organization",
                table: "TeamOperatingModels");
        }
    }
}
