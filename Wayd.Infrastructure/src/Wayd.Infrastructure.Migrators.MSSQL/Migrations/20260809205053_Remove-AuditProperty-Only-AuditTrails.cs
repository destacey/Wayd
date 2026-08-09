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

        // Soft deletes: strip the bookkeeping, keep the row.
        //
        // A soft-delete payload records only IsDeleted/Deleted/DeletedBy, and every one of those is
        // already stated by the row itself — IsDeleted restates Type='SoftDelete', while Deleted and
        // DeletedBy restate DateTime and UserId. Verified against the data: of 66 soft-delete rows,
        // 26 carry a DeletedBy that matches UserId exactly and 40 carry null, with no mismatches;
        // 19 carry a Deleted timestamp within two seconds of DateTime and 47 carry null. Nothing is
        // lost by removing them, and the writer no longer records them.
        //
        // The row itself is kept: the deletion is an event, and Type + DateTime + UserId are the
        // record of it. Only the redundant payload goes.
        //
        // JSON_MODIFY(doc, path, NULL) removes a key outright rather than setting it to JSON null,
        // which is what makes this a strip and not a rewrite. Applied to both the current System*
        // shadow names and the legacy ones that preceded them, since soft-delete rows span both
        // eras. WorkItem is exempt from the legacy names for the same reason as above — they are
        // real synced fields there.
        // The result must match what the fixed writer now produces for the same event, so that a
        // soft delete recorded before this migration is indistinguishable from one recorded after.
        // AuditTrail.ToAuditTrail() serializes an empty collection as NULL, not as '{}' or '[]':
        //
        //     OldValues = OldValues.Count == 0 ? null : SerializeForAudit(OldValues)
        //
        // so a soft delete whose payload was nothing but bookkeeping ends up with all three columns
        // NULL, and the row carries its meaning entirely in Type + DateTime + UserId. NULLIF below
        // collapses the emptied JSON to NULL for exactly that reason.
        //
        // The System* names are stripped here too: the writer excludes them from every entity, so
        // leaving them on these rows would produce the same inconsistency in the other direction.
        // A column that was already NULL stays NULL.
        migrationBuilder.Sql(@"
            UPDATE [Auditing].[AuditTrails]
            SET [OldValues] = CASE WHEN ISJSON([OldValues]) = 1
                                   THEN NULLIF(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY([OldValues],
                                            '$.IsDeleted', NULL), '$.Deleted', NULL), '$.DeletedBy', NULL),
                                            '$.SystemCreated', NULL), '$.SystemCreatedBy', NULL),
                                            '$.SystemLastModified', NULL), '$.SystemLastModifiedBy', NULL), '{}')
                                   ELSE [OldValues] END,
                [NewValues] = CASE WHEN ISJSON([NewValues]) = 1
                                   THEN NULLIF(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY([NewValues],
                                            '$.IsDeleted', NULL), '$.Deleted', NULL), '$.DeletedBy', NULL),
                                            '$.SystemCreated', NULL), '$.SystemCreatedBy', NULL),
                                            '$.SystemLastModified', NULL), '$.SystemLastModifiedBy', NULL), '{}')
                                   ELSE [NewValues] END,
                [AffectedColumns] = CASE
                        WHEN ISJSON([AffectedColumns]) = 1 THEN NULLIF((
                            SELECT ISNULL(
                                '[' + STRING_AGG('""' + STRING_ESCAPE(cols.[value], 'json') + '""', ',')
                                    WITHIN GROUP (ORDER BY cols.[key]) + ']',
                                '[]')
                            FROM (
                                SELECT c.[key], c.[value]
                                FROM OPENJSON([AffectedColumns]) c
                                WHERE LEFT(LTRIM([AffectedColumns]), 1) = '['
                                UNION ALL
                                SELECT c.[key], c.[value]
                                FROM OPENJSON([AffectedColumns], '$.""$values""') c
                                WHERE LEFT(LTRIM([AffectedColumns]), 1) = '{'
                            ) cols
                            WHERE cols.[value] NOT IN ('IsDeleted', 'Deleted', 'DeletedBy',
                                                       'SystemCreated', 'SystemCreatedBy',
                                                       'SystemLastModified', 'SystemLastModifiedBy')
                        ), '[]')
                        ELSE [AffectedColumns] END
            WHERE [Type] = 'SoftDelete'
              AND (ISJSON([OldValues]) = 1 OR ISJSON([NewValues]) = 1 OR ISJSON([AffectedColumns]) = 1);
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
