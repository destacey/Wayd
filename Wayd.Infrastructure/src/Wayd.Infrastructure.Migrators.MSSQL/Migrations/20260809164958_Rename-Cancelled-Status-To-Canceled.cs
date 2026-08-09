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
    // Two different casings are in play, and both are load-bearing:
    //
    //   Entity columns  - PascalCase ('Cancelled'). EnumConverter persists e.ToString().
    //   Audit JSON      - camelCase ('cancelled'). The audit trail captures the CLR enum value and
    //                     serializes it with JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
    //                     so the stored document reads "status":"cancelled".
    //
    // Matching the wrong casing updates nothing and reports success, so each statement below is
    // written against the casing its own column actually uses.

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
        // happens to record a 'cancelled' value of its own is left alone. The replacement targets the
        // full JSON member ("status":"cancelled") rather than the bare word, so a status value is
        // never rewritten inside a name, description, or other free-text field that merely contains it.
        var fromJson = $"\"status\":\"{ToCamelCase(from)}\"";
        var toJson = $"\"status\":\"{ToCamelCase(to)}\"";

        migrationBuilder.Sql($@"
            UPDATE [Auditing].[AuditTrails]
            SET [OldValues] = REPLACE([OldValues], '{fromJson}', '{toJson}'),
                [NewValues] = REPLACE([NewValues], '{fromJson}', '{toJson}')
            WHERE [TableName] IN ('Project', 'Program', 'StrategicInitiative', 'ProjectTask', 'ProjectPhase')
              AND ([OldValues] LIKE '%{fromJson}%' OR [NewValues] LIKE '%{fromJson}%');
        ");
    }

    private static string ToCamelCase(string value) =>
        char.ToLowerInvariant(value[0]) + value[1..];
}
