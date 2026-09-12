using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class MergeGoalsObjectivesIntoPlanningIntervalObjectives : Migration
{
    // Goals.Objectives was only ever read through PlanningIntervalObjectives.ObjectiveId, so its columns
    // move onto the PI objective and the Goals table goes. The audit trail records that as it happened:
    // an Update per PI objective and a Delete per Goals row, under one correlation id so Down can remove
    // exactly the rows this migration wrote. The Goals rows' existing history is not rewritten.

    private const string SystemUserId = "11111111-1111-1111-1111-111111111111"; // SystemIdentity.UserId
    private const string CorrelationId = "7b2d4e8a-3c51-4f96-9a0e-5d1c8f27b364";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. The old indexes INCLUDE ObjectiveId, so they have to go before the column can.
        migrationBuilder.DropIndex(
            name: "IX_PlanningIntervalObjectives_Id_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropIndex(
            name: "IX_PlanningIntervalObjectives_Key_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropIndex(
            name: "IX_PlanningIntervalObjectives_ObjectiveId_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropIndex(
            name: "IX_PlanningIntervalObjectives_PlanningIntervalId_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        // 2. Add the columns that move over from Goals.Objectives. Name is nullable until the
        //    backfill has run; it is tightened to NOT NULL in step 4.
        migrationBuilder.AddColumn<string>(
            name: "Name",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "nvarchar(1024)",
            maxLength: 1024,
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Progress",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "float",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<DateTime>(
            name: "StartDate",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "TargetDate",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ClosedDate",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Order",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "int",
            nullable: true);

        // 3. Copy each PI objective's fields from its Goals objective, and audit both halves of the
        //    move: an Update on every PI objective and a Delete for every Goals row, since the Goals
        //    table is dropped in step 5. The Goals rows' own history is left exactly as it is.
        //
        //    The join ignores the Goals row's IsDeleted on purpose: the PI row is the one that decides
        //    whether the objective is live, and a soft-deleted Goals row behind a live PI row still
        //    holds its name. Status is not copied — the PI row already carries the status the app shows.
        //    A PI row with no Goals row behind it (the old two-step create could leave one when the
        //    second save failed), or whose Goals name is blank, gets a placeholder name: the entity
        //    rejects a blank name on load, so a blank copied through would make the row unreadable.
        //
        //    Audit rows mirror what AuditTrail.ToAuditTrail writes: TableName is the CLR entity name,
        //    PrimaryKey/OldValues/NewValues are camelCase JSON with lowercase GUIDs and nulls omitted,
        //    AffectedColumns is a JSON array of property names.
        migrationBuilder.Sql($@"
                DECLARE @Now datetime2(7) = SYSUTCDATETIME();

                -- Capture the new values before mutating, so the update and the audit rows share one source.
                SELECT
                    po.[Id],
                    po.[Key],
                    CASE WHEN o.[Id] IS NULL THEN 0 ELSE 1 END AS Matched,
                    ISNULL(NULLIF(LTRIM(RTRIM(o.[Name])), N''), N'Objective ' + CAST(po.[Key] AS nvarchar(20))) AS [Name],
                    o.[Description],
                    ISNULL(o.[Progress], 0.0) AS [Progress],
                    o.[StartDate],
                    o.[TargetDate],
                    o.[ClosedDate],
                    o.[Order]
                INTO #ObjectiveMerge
                FROM [Planning].[PlanningIntervalObjectives] AS po
                LEFT JOIN [Goals].[Objectives] AS o ON o.[Id] = po.[ObjectiveId];

                UPDATE po
                SET
                    po.[Name] = m.[Name],
                    po.[Description] = m.[Description],
                    po.[Progress] = m.[Progress],
                    po.[StartDate] = m.[StartDate],
                    po.[TargetDate] = m.[TargetDate],
                    po.[ClosedDate] = m.[ClosedDate],
                    po.[Order] = m.[Order]
                FROM [Planning].[PlanningIntervalObjectives] AS po
                INNER JOIN #ObjectiveMerge AS m ON m.[Id] = po.[Id];

                -- One Update trail per PI objective. Before the backfill every merged column was null
                -- except Progress, which the column default set to 0.
                INSERT INTO [Auditing].[AuditTrails]
                    ([Id], [UserId], [Type], [SchemaName], [TableName], [DateTime], [OldValues], [NewValues], [AffectedColumns], [PrimaryKey], [CorrelationId])
                SELECT
                    NEWID(),
                    '{SystemUserId}',
                    'Update',
                    'Planning',
                    'PlanningIntervalObjective',
                    @Now,
                    '{{""progress"":0}}',
                    '{{""name"":""' + STRING_ESCAPE(m.[Name], 'json') + '""'
                        + CASE WHEN m.[Description] IS NULL THEN '' ELSE ',""description"":""' + STRING_ESCAPE(m.[Description], 'json') + '""' END
                        + ',""progress"":' + CONVERT(varchar(32), m.[Progress])
                        + CASE WHEN m.[StartDate] IS NULL THEN '' ELSE ',""startDate"":""' + CONVERT(varchar(10), m.[StartDate], 23) + '""' END
                        + CASE WHEN m.[TargetDate] IS NULL THEN '' ELSE ',""targetDate"":""' + CONVERT(varchar(10), m.[TargetDate], 23) + '""' END
                        + CASE WHEN m.[ClosedDate] IS NULL THEN '' ELSE ',""closedDate"":""' + CONVERT(varchar(27), m.[ClosedDate], 127) + 'Z""' END
                        + CASE WHEN m.[Order] IS NULL THEN '' ELSE ',""order"":' + CONVERT(varchar(11), m.[Order]) END
                        + '}}',
                    CASE WHEN m.Matched = 1
                        THEN '[""Name"",""Description"",""Progress"",""StartDate"",""TargetDate"",""ClosedDate"",""Order""]'
                        ELSE '[""Name""]' END,
                    '{{""id"":""' + LOWER(CONVERT(varchar(36), m.[Id])) + '""}}',
                    '{CorrelationId}'
                FROM #ObjectiveMerge AS m;

                DROP TABLE #ObjectiveMerge;

                -- One Delete trail per Goals row, whether or not a PI objective pointed at it: every row
                -- goes with the table. OldValues is the row as it stood, so the trail is the last record of it.
                INSERT INTO [Auditing].[AuditTrails]
                    ([Id], [UserId], [Type], [SchemaName], [TableName], [DateTime], [OldValues], [NewValues], [AffectedColumns], [PrimaryKey], [CorrelationId])
                SELECT
                    NEWID(),
                    '{SystemUserId}',
                    'Delete',
                    'Goals',
                    'Objective',
                    @Now,
                    '{{""id"":""' + LOWER(CONVERT(varchar(36), o.[Id])) + '""'
                        + ',""key"":' + CONVERT(varchar(11), o.[Key])
                        + ',""name"":""' + STRING_ESCAPE(o.[Name], 'json') + '""'
                        + CASE WHEN o.[Description] IS NULL THEN '' ELSE ',""description"":""' + STRING_ESCAPE(o.[Description], 'json') + '""' END
                        + ',""type"":""' + o.[Type] + '""'
                        + ',""status"":""' + o.[Status] + '""'
                        + ',""progress"":' + CONVERT(varchar(32), o.[Progress])
                        + CASE WHEN o.[OwnerId] IS NULL THEN '' ELSE ',""ownerId"":""' + LOWER(CONVERT(varchar(36), o.[OwnerId])) + '""' END
                        + CASE WHEN o.[PlanId] IS NULL THEN '' ELSE ',""planId"":""' + LOWER(CONVERT(varchar(36), o.[PlanId])) + '""' END
                        + CASE WHEN o.[StartDate] IS NULL THEN '' ELSE ',""startDate"":""' + CONVERT(varchar(10), o.[StartDate], 23) + '""' END
                        + CASE WHEN o.[TargetDate] IS NULL THEN '' ELSE ',""targetDate"":""' + CONVERT(varchar(10), o.[TargetDate], 23) + '""' END
                        + CASE WHEN o.[ClosedDate] IS NULL THEN '' ELSE ',""closedDate"":""' + CONVERT(varchar(27), o.[ClosedDate], 127) + 'Z""' END
                        + CASE WHEN o.[Order] IS NULL THEN '' ELSE ',""order"":' + CONVERT(varchar(11), o.[Order]) END
                        + ',""isDeleted"":' + CASE WHEN o.[IsDeleted] = 1 THEN 'true' ELSE 'false' END
                        + '}}',
                    NULL,
                    NULL,
                    '{{""id"":""' + LOWER(CONVERT(varchar(36), o.[Id])) + '""}}',
                    '{CorrelationId}'
                FROM [Goals].[Objectives] AS o;
            ");

        // 4. Every row has a name now.
        migrationBuilder.AlterColumn<string>(
            name: "Name",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(256)",
            oldMaxLength: 256,
            oldNullable: true);

        // 5. Drop the link and the Goals table it pointed at.
        migrationBuilder.DropColumn(
            name: "ObjectiveId",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropTable(
            name: "Objectives",
            schema: "Goals");

        migrationBuilder.Sql("DROP SCHEMA IF EXISTS [Goals];");

        // 6. Recreate the covering indexes over the merged columns.
        migrationBuilder.CreateIndex(
            name: "IX_PlanningIntervalObjectives_Id_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            columns: new[] { "Id", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Key", "PlanningIntervalId", "Name", "Type", "Status", "IsStretch", "Order" });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningIntervalObjectives_Key_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            columns: new[] { "Key", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id", "PlanningIntervalId", "Name", "Type", "Status", "IsStretch", "Order" });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningIntervalObjectives_PlanningIntervalId_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            columns: new[] { "PlanningIntervalId", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "Name", "Type", "Status", "IsStretch", "Order" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"DELETE FROM [Auditing].[AuditTrails] WHERE [CorrelationId] = '{CorrelationId}';");

        migrationBuilder.DropIndex(
            name: "IX_PlanningIntervalObjectives_Id_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropIndex(
            name: "IX_PlanningIntervalObjectives_Key_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropIndex(
            name: "IX_PlanningIntervalObjectives_PlanningIntervalId_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.EnsureSchema(
            name: "Goals");

        migrationBuilder.CreateTable(
            name: "Objectives",
            schema: "Goals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                Deleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                Key = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Order = table.Column<int>(type: "int", nullable: true),
                OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Progress = table.Column<double>(type: "float", nullable: false),
                StartDate = table.Column<DateTime>(type: "date", nullable: true),
                Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                TargetDate = table.Column<DateTime>(type: "date", nullable: true),
                Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Objectives", x => x.Id);
                table.UniqueConstraint("AK_Objectives_Key", x => x.Key);
            });

        // The original Goals ids are gone, so each PI objective gets a fresh one and a Goals row
        // rebuilt from the merged columns. Every rebuilt row is of type PlanningInterval, which is
        // the only type the old table ever held.
        migrationBuilder.AddColumn<Guid>(
            name: "ObjectiveId",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.Sql(@"
                UPDATE [Planning].[PlanningIntervalObjectives] SET [ObjectiveId] = NEWID();

                INSERT INTO [Goals].[Objectives]
                    ([Id], [Name], [Description], [Type], [Status], [Progress], [OwnerId], [PlanId],
                     [StartDate], [TargetDate], [ClosedDate], [Order],
                     [Deleted], [DeletedBy], [IsDeleted],
                     [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy])
                SELECT
                    po.[ObjectiveId], po.[Name], po.[Description], N'PlanningInterval', po.[Status], po.[Progress], po.[TeamId], po.[PlanningIntervalId],
                    po.[StartDate], po.[TargetDate], po.[ClosedDate], po.[Order],
                    po.[Deleted], po.[DeletedBy], po.[IsDeleted],
                    po.[SystemCreated], po.[SystemCreatedBy], po.[SystemLastModified], po.[SystemLastModifiedBy]
                FROM [Planning].[PlanningIntervalObjectives] AS po;
            ");

        migrationBuilder.DropColumn(
            name: "ClosedDate",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropColumn(
            name: "Description",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropColumn(
            name: "Name",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropColumn(
            name: "Order",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropColumn(
            name: "Progress",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropColumn(
            name: "StartDate",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.DropColumn(
            name: "TargetDate",
            schema: "Planning",
            table: "PlanningIntervalObjectives");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningIntervalObjectives_Id_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            columns: new[] { "Id", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Key", "PlanningIntervalId", "ObjectiveId", "Type", "IsStretch" });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningIntervalObjectives_Key_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            columns: new[] { "Key", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id", "PlanningIntervalId", "ObjectiveId", "Type", "IsStretch" });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningIntervalObjectives_ObjectiveId_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            columns: new[] { "ObjectiveId", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "PlanningIntervalId", "Type", "IsStretch" });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningIntervalObjectives_PlanningIntervalId_IsDeleted",
            schema: "Planning",
            table: "PlanningIntervalObjectives",
            columns: new[] { "PlanningIntervalId", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "ObjectiveId", "Type", "IsStretch" });

        migrationBuilder.CreateIndex(
            name: "IX_Objectives_Id_IsDeleted",
            schema: "Goals",
            table: "Objectives",
            columns: new[] { "Id", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Key", "Name", "Type", "Status", "OwnerId", "PlanId", "Order" });

        migrationBuilder.CreateIndex(
            name: "IX_Objectives_Key_IsDeleted",
            schema: "Goals",
            table: "Objectives",
            columns: new[] { "Key", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id", "Name", "Type", "Status", "OwnerId", "PlanId", "Order" });

        migrationBuilder.CreateIndex(
            name: "IX_Objectives_OwnerId_IsDeleted",
            schema: "Goals",
            table: "Objectives",
            columns: new[] { "OwnerId", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "Name", "Type", "Status", "PlanId", "Order" });

        migrationBuilder.CreateIndex(
            name: "IX_Objectives_PlanId_IsDeleted",
            schema: "Goals",
            table: "Objectives",
            columns: new[] { "PlanId", "IsDeleted" },
            filter: "[IsDeleted] = 0")
            .Annotation("SqlServer:Include", new[] { "Id", "Key", "Name", "Type", "Status", "OwnerId", "Order" });
    }
}
