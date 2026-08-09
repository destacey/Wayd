using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class RenameCancelledStatusToCanceled : Migration
{
    // Renames the PPM status member Cancelled -> Canceled everywhere it is persisted. The intent of the
    // status does not change; only its spelling does, to match the American spelling already used by
    // objectives and by LifecycleCategory.Canceled.
    //
    // This migration is mandatory, not cosmetic. These status columns are varchar, written by
    // EnumConverter as the enum MEMBER NAME and read back with Enum.Parse, so a row still holding
    // 'Cancelled' after the rename throws on read rather than degrading quietly.
    //
    // Three casings are in play, and all are load-bearing:
    //
    //   Entity columns  - PascalCase ('Cancelled'). EnumConverter persists e.ToString().
    //
    //   Audit JSON KEY  - PascalCase ("Status"). OldValues/NewValues are built from a
    //                     Dictionary<string, object?> keyed by the EF property name.
    //                     SerializeForAudit sets PropertyNamingPolicy = CamelCase, which renames
    //                     reflected POCO properties but NOT dictionary keys — those need
    //                     DictionaryKeyPolicy, which is not set. (Same caveat documented in
    //                     20260809132517_Fix-Project-Rank-Backfill-AuditPrimaryKey, verified
    //                     against real rows.)
    //
    //   Audit JSON VALUE- camelCase ('cancelled'). The enum value IS renamed, by
    //                     JsonStringEnumConverter(JsonNamingPolicy.CamelCase).
    //
    // So the stored audit document is mixed-case:
    //
    //     {"Status":"cancelled","SystemLastModified":"2026-06-27T14:20:09Z"}
    //
    // Matching the wrong casing updates nothing and reports success, so each statement below is
    // written against the casing its own column actually uses.
    //
    // The audit rewrite uses JSON_MODIFY rather than a string REPLACE. REPLACE cannot tell a key
    // from a value, and it follows the column collation: under the default case-INSENSITIVE
    // collation a REPLACE of '"status":"cancelled"' matches '"Status":"cancelled"' but substitutes
    // the literal lowercase key, DOWNCASING the key while fixing the value. JSON_MODIFY rewrites
    // through the JSON parser, so it can only touch the value — it cannot corrupt a key, and it
    // cannot hit the word 'cancelled' inside a name, description, or other free-text field.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        RewriteStatuses(migrationBuilder, from: "Cancelled", to: "Canceled");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RewriteStatuses(migrationBuilder, from: "Canceled", to: "Cancelled");
    }

    private static void RewriteStatuses(MigrationBuilder migrationBuilder, string from, string to)
    {
        // Entity rows. Equality (not LIKE) so only the whole status value is touched.
        migrationBuilder.Sql($@"
            UPDATE [Ppm].[Projects]              SET [Status] = '{to}' WHERE [Status] = '{from}';
            UPDATE [Ppm].[Programs]              SET [Status] = '{to}' WHERE [Status] = '{from}';
            UPDATE [Ppm].[StrategicInitiatives]  SET [Status] = '{to}' WHERE [Status] = '{from}';
            UPDATE [Ppm].[ProjectTasks]          SET [Status] = '{to}' WHERE [Status] = '{from}';
            UPDATE [Ppm].[ProjectPhases]         SET [Status] = '{to}' WHERE [Status] = '{from}';
        ");

        // Audit history. The old and new values are preserved as written — only the spelling of the
        // status token changes, so what each trail row says happened is unchanged.
        //
        // Scoped to the five entities whose status this rename covers, so an unrelated entity that
        // happens to record a 'cancelled' value of its own is left alone.
        //
        // Both key casings are handled because the trail is not perfectly uniform: a case-sensitive
        // count over Project trails found 134 rows keyed "Status" and 1 keyed "status". Only the
        // VALUE is normalized; each row keeps whatever key casing it was written with, since
        // repairing key casing is a separate concern from this rename.
        //
        // The value comparison carries an explicit case-sensitive collation so it behaves the same
        // regardless of the database's own collation, and so a row already reading 'canceled' is
        // left alone (making this re-runnable).
        var fromValue = ToCamelCase(from);
        var toValue = ToCamelCase(to);

        foreach (var key in new[] { "Status", "status" })
        {
            migrationBuilder.Sql($@"
                UPDATE [Auditing].[AuditTrails]
                SET [OldValues] = CASE
                        WHEN ISJSON([OldValues]) = 1
                         AND JSON_VALUE([OldValues], '$.{key}') COLLATE Latin1_General_CS_AS = '{fromValue}'
                        THEN JSON_MODIFY([OldValues], '$.{key}', '{toValue}')
                        ELSE [OldValues]
                    END,
                    [NewValues] = CASE
                        WHEN ISJSON([NewValues]) = 1
                         AND JSON_VALUE([NewValues], '$.{key}') COLLATE Latin1_General_CS_AS = '{fromValue}'
                        THEN JSON_MODIFY([NewValues], '$.{key}', '{toValue}')
                        ELSE [NewValues]
                    END
                WHERE [TableName] IN ('Project', 'Program', 'StrategicInitiative', 'ProjectTask', 'ProjectPhase')
                  AND (
                        (ISJSON([OldValues]) = 1
                         AND JSON_VALUE([OldValues], '$.{key}') COLLATE Latin1_General_CS_AS = '{fromValue}')
                     OR (ISJSON([NewValues]) = 1
                         AND JSON_VALUE([NewValues], '$.{key}') COLLATE Latin1_General_CS_AS = '{fromValue}')
                      );
            ");
        }
    }

    private static string ToCamelCase(string value) =>
        char.ToLowerInvariant(value[0]) + value[1..];
}
