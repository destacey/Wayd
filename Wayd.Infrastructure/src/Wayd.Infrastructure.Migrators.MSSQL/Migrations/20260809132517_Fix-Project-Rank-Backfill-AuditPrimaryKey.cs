using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class FixProjectRankBackfillAuditPrimaryKey : Migration
{
    // 20260607161829_Make-Project-Rank-Required backfilled Ppm.Projects.Rank and hand-wrote an audit
    // row per changed project, but wrote PrimaryKey as a bare GUID:
    //
    //     CONVERT(varchar(450), b.[Id])          -> 5C0C9A1E-...  (bare, uppercase)
    //
    // Every other row in the table — and everything the application writes at runtime — stores that
    // column as the serialized key dictionary, PascalCase with a lowercase GUID:
    //
    //     {"Id":"5c0c9a1e-..."}
    //
    // The shape comes from AuditTrail.ToAuditTrail: PrimaryKey = SerializeForAudit(KeyValues), where
    // KeyValues is a Dictionary keyed by the EF property name. The serializer's CamelCase policy
    // applies to POCO properties, NOT dictionary keys (that would need DictionaryKeyPolicy, which is
    // not set), so the key stays "Id". Guid.ToString() is lowercase; SQL's CONVERT is uppercase,
    // hence LOWER(). The same convention is spelled out in 20260803012712_Add-Unique-Users-
    // EmployeeId-Index and 20260808171822_BackfillEmployeeEmails, both verified against real rows.
    //
    // The malformed rows never match a lookup by primary key, so those projects appear to have no
    // rank-change history: the audit UI filters on the JSON form, and the index on PrimaryKey makes
    // an equality match the expected access path. This migration rewrites the affected rows in place
    // rather than amending the original migration, which EF will never re-run where it has already
    // been applied — and it has, in every database carrying the migrations that follow it.

    private const string BackfillCorrelationId = "2fc3efb1-dfcb-4735-bc29-1c311980d443"; // the id 20260607161829 stamped on the rows it wrote.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Scoped by the original migration's correlation id, so only rows it wrote are touched.
        // The TRY_CONVERT guard makes this re-runnable and skips any row already in JSON form:
        // a bare GUID converts, '{"Id":"..."}' does not.
        migrationBuilder.Sql($@"
            UPDATE [Auditing].[AuditTrails]
            SET [PrimaryKey] = '{{""Id"":""' + LOWER(CONVERT(varchar(36), TRY_CONVERT(uniqueidentifier, [PrimaryKey]))) + '""}}'
            WHERE [CorrelationId] = '{BackfillCorrelationId}'
              AND [SchemaName] = 'Ppm'
              AND [TableName] = 'Project'
              AND [Type] = 'Update'
              AND [AffectedColumns] = '[""Rank""]'
              AND TRY_CONVERT(uniqueidentifier, [PrimaryKey]) IS NOT NULL;
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately empty. Reverting would mean rewriting these rows back to the bare-GUID form,
        // which is not safely expressible: nothing distinguishes a row this migration corrected from
        // one that was already in the correct JSON form under the same correlation id, so a blanket
        // reverse update would corrupt rows that were never broken. (Verified — an earlier attempt at
        // a reversing Down did exactly that to a pre-existing well-formed row.)
        //
        // Leaving the corrected rows in place is also harmless, because 20260607161829's Down now
        // matches both shapes: rolling that migration back still finds and reverts its own rows.
    }
}
