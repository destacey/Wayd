using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class ScrubProtectedAuditTrailValues : Migration
    {
        /// <summary>
        /// Removes from existing audit trail rows the values the trail no longer records: a connection's whole
        /// configuration, whose secrets were written in plain text although their own column is encrypted, the
        /// reason for an employee's time away, and a personal access token's hash. The trail row, its affected
        /// columns and every other value are kept.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A configuration is removed whole rather than re-encrypted: T-SQL cannot reach the DataProtection key, and
        /// leaving the non-secret settings would mean naming every secret field of every connector, past and present.
        /// It has been written under two keys: <c>ConfigurationString</c>, a JSON string, until the column became a
        /// mapped object in September 2023, and <c>Configuration</c>, an object, since. Both are removed, which is
        /// why each test is <c>JSON_VALUE</c> (a string) or <c>JSON_QUERY</c> (an object). The connection table was
        /// briefly named for connectors, so both type-name suffixes are matched.
        /// </para>
        /// <para>
        /// Rows have been written with both camelCase and PascalCase keys, and some wrap the payload in a
        /// <c>$id</c> envelope; the keys stay top-level either way, so each is removed under both spellings. JSON
        /// paths are case-sensitive, and removing a key that is absent changes nothing. A payload left as an empty
        /// object is set to null, as the writer leaves it; an envelope keeps its <c>$id</c>. Rows that are not valid
        /// JSON are left alone, and no JSON function ever sees one: SQL Server does not promise to evaluate an
        /// <c>ISJSON</c> filter first, so each reads a copy that is null unless the row is valid. One statement, so the table is scanned once.
        /// </para>
        /// <para>
        /// Meant to run once, before the trail records configurations encrypted: run again, it would remove those too.
        /// </para>
        /// </remarks>
        public const string ScrubSql = """
            WITH [Candidate] AS (
                SELECT [OldValues], [NewValues],
                       CASE WHEN [TableName] LIKE '%Connection' OR [TableName] LIKE '%Connector' THEN 'Connection'
                            ELSE [TableName] END AS [Kind],
                       CASE WHEN ISJSON(ISNULL([OldValues], '{}')) = 1 AND ISJSON(ISNULL([NewValues], '{}')) = 1
                            THEN 1 ELSE 0 END AS [IsJson]
                FROM [Auditing].[AuditTrails]
                WHERE [TableName] LIKE '%Connection' OR [TableName] LIKE '%Connector'
                   OR [TableName] IN ('EmployeeTimeAway', 'PersonalAccessToken')
            ),
            [Scrub] AS (
                SELECT [OldValues], [NewValues], [Kind], [IsJson],
                       CASE WHEN [IsJson] = 1 THEN [OldValues] END AS [OldJson],
                       CASE WHEN [IsJson] = 1 THEN [NewValues] END AS [NewJson]
                FROM [Candidate]
            )
            UPDATE [Scrub]
            SET [OldValues] = CASE [Kind]
                    WHEN 'Connection' THEN NULLIF(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY([OldJson],
                        '$.configuration', NULL), '$.Configuration', NULL), '$.configurationString', NULL), '$.ConfigurationString', NULL), '{}')
                    WHEN 'EmployeeTimeAway' THEN NULLIF(JSON_MODIFY(JSON_MODIFY([OldJson], '$.reason', NULL), '$.Reason', NULL), '{}')
                    ELSE NULLIF(JSON_MODIFY(JSON_MODIFY([OldJson], '$.tokenHash', NULL), '$.TokenHash', NULL), '{}')
                END,
                [NewValues] = CASE [Kind]
                    WHEN 'Connection' THEN NULLIF(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY([NewJson],
                        '$.configuration', NULL), '$.Configuration', NULL), '$.configurationString', NULL), '$.ConfigurationString', NULL), '{}')
                    WHEN 'EmployeeTimeAway' THEN NULLIF(JSON_MODIFY(JSON_MODIFY([NewJson], '$.reason', NULL), '$.Reason', NULL), '{}')
                    ELSE NULLIF(JSON_MODIFY(JSON_MODIFY([NewJson], '$.tokenHash', NULL), '$.TokenHash', NULL), '{}')
                END
            WHERE [IsJson] = 1 AND (([Kind] = 'Connection' AND (
                       JSON_QUERY([OldJson], '$.configuration') IS NOT NULL OR JSON_QUERY([OldJson], '$.Configuration') IS NOT NULL
                    OR JSON_QUERY([NewJson], '$.configuration') IS NOT NULL OR JSON_QUERY([NewJson], '$.Configuration') IS NOT NULL
                    OR JSON_VALUE([OldJson], '$.configuration') IS NOT NULL OR JSON_VALUE([OldJson], '$.Configuration') IS NOT NULL
                    OR JSON_VALUE([NewJson], '$.configuration') IS NOT NULL OR JSON_VALUE([NewJson], '$.Configuration') IS NOT NULL
                    OR JSON_VALUE([OldJson], '$.configurationString') IS NOT NULL OR JSON_VALUE([OldJson], '$.ConfigurationString') IS NOT NULL
                    OR JSON_VALUE([NewJson], '$.configurationString') IS NOT NULL OR JSON_VALUE([NewJson], '$.ConfigurationString') IS NOT NULL))
               OR ([Kind] = 'EmployeeTimeAway' AND (
                       JSON_VALUE([OldJson], '$.reason') IS NOT NULL OR JSON_VALUE([OldJson], '$.Reason') IS NOT NULL
                    OR JSON_VALUE([NewJson], '$.reason') IS NOT NULL OR JSON_VALUE([NewJson], '$.Reason') IS NOT NULL))
               OR ([Kind] = 'PersonalAccessToken' AND (
                       JSON_VALUE([OldJson], '$.tokenHash') IS NOT NULL OR JSON_VALUE([OldJson], '$.TokenHash') IS NOT NULL
                    OR JSON_VALUE([NewJson], '$.tokenHash') IS NOT NULL OR JSON_VALUE([NewJson], '$.TokenHash') IS NOT NULL)));
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ScrubSql);
        }

        /// <inheritdoc />
        /// <remarks>Not undone: the removed values are gone, which is the point.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
