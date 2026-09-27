using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamOfTeamsOperatingModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamOfTeamsOperatingModels",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    End = table.Column<DateTime>(type: "date", nullable: true),
                    Start = table.Column<DateTime>(type: "date", nullable: false),
                    TimeZone = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamOfTeamsOperatingModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamOfTeamsOperatingModels_Teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Organization",
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamOfTeamsOperatingModels_TeamId_Current",
                schema: "Organization",
                table: "TeamOfTeamsOperatingModels",
                column: "TeamId",
                filter: "[End] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Id", "TimeZone" });

            // TeamOfTeams.Create opens a model from now on; every existing team of teams, including a
            // soft-deleted one that could be restored, gets one from its active date.
            migrationBuilder.Sql(@"
                INSERT INTO [Organization].[TeamOfTeamsOperatingModels]
                    (Id, TeamId, Start, [End], TimeZone, SystemCreated, SystemLastModified)
                SELECT
                    NEWID(),
                    t.Id,
                    t.ActiveDate,
                    NULL,
                    'UTC',
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM [Organization].[Teams] t
                WHERE t.Type = 'TeamOfTeams'
                    AND NOT EXISTS (
                        SELECT 1 FROM [Organization].[TeamOfTeamsOperatingModels] m
                        WHERE m.TeamId = t.Id
                    );
            ");

            migrationBuilder.Sql(RecordBackfilledModelsSql);
        }

        // SystemUser.Id, frozen here: a migration must keep writing what it wrote when it shipped.
        internal const string SystemUserId = "11111111-1111-1111-1111-111111111111";

        // Records each backfilled model as the TeamOfTeamsOperatingModelSetEvent a live Create raises, so a team of
        // teams' log starts from a known zone. The payload is written by hand, frozen at the 1.0 shape, and has to
        // read back exactly as the serializer would have written it, including FlexibleDateRange's computed
        // EffectiveEnd and Days. TeamOfTeamsOperatingModelBackfillTests compares it with ActivityLogEntryFactory's.
        //
        // The model row's id is the EventId, so the entry is tied to the model it records and cannot be written
        // twice. Soft-deleted teams are skipped, as the baseline backfill skipped them: their history ends with
        // their deletion, and an entry written now would follow it.
        internal static string RecordBackfilledModelsSql => $$"""
            DECLARE @Now datetime2(7) = SYSUTCDATETIME();

            INSERT INTO [App].[ActivityLogs]
            (
                [EventId], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary]
            )
            SELECT
                m.[Id],
                'TeamOfTeamsOperatingModelSetEvent',
                'Updated',
                '1.0',
                'Organization',
                'Team',
                t.[Id],
                'System',
                '{{SystemUserId}}',
                NULL,
                @Now,
                0,
                NULL,
                CAST(N'{"id":"' AS nvarchar(max)) + LOWER(CONVERT(nvarchar(36), t.[Id]))
                    + N'","key":' + CONVERT(nvarchar(11), t.[Key])
                    + N',"period":{"start":"' + CONVERT(nchar(10), m.[Start], 23)
                        + N'","end":null,"effectiveEnd":"9999-12-31","days":'
                        + CONVERT(nvarchar(11), DATEDIFF(DAY, m.[Start], '9999-12-31') + 1) + N'}'
                    + N',"settings":{"timeZone":"' + STRING_ESCAPE(m.[TimeZone], 'json') + N'"}'
                    + N',"supersededPeriod":null'
                    + N',"timestamp":"' + {{Iso("@Now")}} + N'"'
                    + N',"eventId":"' + LOWER(CONVERT(nvarchar(36), m.[Id])) + N'"'
                    + N',"actor":{"kind":"system","userId":"{{SystemUserId}}","employeeId":null}'
                    + N',"eventVersion":"1.0"}',
                N'Team Of Teams Operating Model Set'
            FROM [Organization].[TeamOfTeamsOperatingModels] m
            INNER JOIN [Organization].[Teams] t ON t.[Id] = m.[TeamId]
            WHERE t.[IsDeleted] = 0
              AND NOT EXISTS (SELECT 1 FROM [App].[ActivityLogs] a
                              WHERE a.[AggregateType] = 'Team' AND a.[AggregateId] = t.[Id]
                                AND a.[EventType] = 'TeamOfTeamsOperatingModelSetEvent');
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE a
                FROM [App].[ActivityLogs] a
                INNER JOIN [Organization].[TeamOfTeamsOperatingModels] m ON m.[Id] = a.[EventId]
                WHERE a.[EventType] = 'TeamOfTeamsOperatingModelSetEvent';
            ");

            migrationBuilder.DropTable(
                name: "TeamOfTeamsOperatingModels",
                schema: "Organization");
        }
    }
}
