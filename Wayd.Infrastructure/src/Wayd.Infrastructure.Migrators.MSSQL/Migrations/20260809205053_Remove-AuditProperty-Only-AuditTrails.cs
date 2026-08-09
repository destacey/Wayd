using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class RemoveAuditPropertyOnlyAuditTrails : Migration
{
    // Deletes audit rows that record nothing but audit bookkeeping.
    //
    // Until the accompanying change to BaseDbContext, the trail captured every property on the
    // entity, including the audit shadow columns the trail row itself already carries. An entity
    // saved with no real field change therefore produced a row like:
    //
    //     Type=Update  AffectedColumns=["SystemLastModified"]
    //     NewValues={"SystemLastModified":"2026-08-08T15:08:33.9667897Z"}
    //
    // which says only "this was touched at this time by this user" — exactly what the row's own
    // DateTime and UserId already say. Those rows are removed here; the writer no longer creates
    // them.
    //
    // WHAT COUNTS AS BOOKKEEPING
    //
    //   SystemCreated / SystemCreatedBy / SystemLastModified / SystemLastModifiedBy
    //       The shadow properties declared for every ISystemAuditable entity.
    //
    //   Created / CreatedBy / LastModified / LastModifiedBy
    //       The same concept before the System prefix was adopted. Bookkeeping on every entity
    //       EXCEPT WorkItem, which declares real fields of these names synced from Azure DevOps.
    //       That carve-out is load-bearing: without it this would delete genuine work-item history.
    //       Verified against the data — on WorkItem these values diverge from the trail timestamp by
    //       up to 31 days, while on every other table the difference is 0.
    //
    //   IsDeleted / Deleted / DeletedBy
    //       ISoftDelete's bookkeeping. Only treated as noise on rows that are NOT themselves a
    //       soft delete or restore: on those, the trail type is the record of what happened.
    //
    // Name matching relies on the database's case-insensitive collation, which covers both the
    // PascalCase and camelCase spellings present in the data. (Contrast the Cancelled -> Canceled
    // rename, where the VALUE casing was load-bearing and needed an explicit CS collation.)
    //
    // TWO SHAPES OF AffectedColumns
    //
    // Most rows store a plain array; rows written between 2024-10 and 2025-10 store a
    // ReferenceHandler.Preserve envelope:
    //
    //     ["LastModified"]                            <- plain
    //     {"$id":"1","$values":["LastModified"]}      <- envelope
    //
    // OPENJSON over the envelope yields the object's KEYS ($id, $values), not the array elements, so
    // a classifier that does not unwrap it reads those rows as having real changes and keeps them.
    // Both shapes are handled below. "$values" is bracket-quoted in the JSON path because a bare $
    // is the path root.
    //
    // SCOPE: only Type='Update'.
    //
    // Some Create, Delete and SoftDelete rows do have payloads consisting solely of bookkeeping
    // properties, and are still kept deliberately. For those types the ROW'S EXISTENCE is the
    // information: it records that an entity was created or removed, which nothing else captures and
    // which cannot be reconstructed. An Update row with no real changed column records nothing at
    // all — the row's own DateTime and UserId already say the entity was touched — which is what
    // makes it safe to delete and those safe to keep.
    //
    // They are also shaped differently: Create and Delete rows carry no AffectedColumns whatsoever,
    // so classifying them would mean inspecting payload keys instead — a different rule with
    // different failure modes, and no benefit.
    //
    // Rows whose AffectedColumns is null or unparseable are left alone rather than guessed at.
    //
    // This is not reversible — see Down.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Batched so a large history does not run as one long transaction, and so the log can be
        // truncated between batches. Re-runnable: a second run matches nothing.
        migrationBuilder.Sql(@"
            SET NOCOUNT ON;

            DECLARE @BatchSize int = 5000;
            DECLARE @Deleted int = 1;

            WHILE @Deleted > 0
            BEGIN
                DELETE TOP (@BatchSize) t
                FROM [Auditing].[AuditTrails] t
                WHERE t.[Type] = 'Update'
                  AND t.[AffectedColumns] IS NOT NULL
                  AND ISJSON(t.[AffectedColumns]) = 1
                  AND NOT EXISTS (
                        SELECT 1
                        FROM (
                            /* plain array shape */
                            SELECT c.[value]
                            FROM OPENJSON(t.[AffectedColumns]) c
                            WHERE LEFT(LTRIM(t.[AffectedColumns]), 1) = '['

                            UNION ALL

                            /* ReferenceHandler.Preserve envelope shape */
                            SELECT c.[value]
                            FROM OPENJSON(t.[AffectedColumns], '$.""$values""') c
                            WHERE LEFT(LTRIM(t.[AffectedColumns]), 1) = '{'
                        ) cols
                        WHERE NOT (
                            cols.[value] IN ('SystemCreated', 'SystemCreatedBy',
                                             'SystemLastModified', 'SystemLastModifiedBy')
                            OR (t.[TableName] <> 'WorkItem'
                                AND cols.[value] IN ('Created', 'CreatedBy',
                                                     'LastModified', 'LastModifiedBy'))
                            OR cols.[value] IN ('IsDeleted', 'Deleted', 'DeletedBy')
                        )
                  );

                SET @Deleted = @@ROWCOUNT;
            END
        ");

        // Soft deletes: clear the payload, keep the row.
        //
        // The payload can only ever hold audit bookkeeping. A soft delete originates from
        // EntityState.Deleted, which BaseDbContext rewrites to Modified with IsDeleted = true — so
        // the only properties that change are ISoftDelete's IsDeleted/Deleted/DeletedBy plus the
        // System* shadow columns. All of those restate what the row already carries: IsDeleted
        // restates Type='SoftDelete', and Deleted/DeletedBy restate DateTime/UserId.
        //
        // Nulling all three columns therefore loses nothing, and matches what the fixed writer now
        // produces: ToAuditTrail serializes an empty collection as NULL rather than '{}' or '[]', so
        // a soft delete recorded before this migration is indistinguishable from one recorded after.
        //
        // Setting the columns outright also covers payload shapes that key-level stripping would
        // miss, such as the ReferenceHandler.Preserve envelope described above.
        migrationBuilder.Sql(@"
            UPDATE [Auditing].[AuditTrails]
            SET [OldValues] = NULL,
                [NewValues] = NULL,
                [AffectedColumns] = NULL
            WHERE [Type] = 'SoftDelete'
              AND ([OldValues] IS NOT NULL OR [NewValues] IS NOT NULL OR [AffectedColumns] IS NOT NULL);
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately empty. The deleted rows recorded only that an entity was touched, which the
        // surviving rows' own DateTime and UserId already convey; there is nothing to reconstruct
        // them from and nothing of value to restore.
    }
}
