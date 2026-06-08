using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class MakeProjectRankRequired : Migration
{
    // Projects are now ranked on creation and Rank is non-nullable. This migration converts the
    // legacy nullable int Rank to a required fractional (float) sort key in three steps:
    //   1. widen the column to nullable float,
    //   2. backfill a clean, gap-free, whole-number ranking per portfolio (every project),
    //      writing a matching audit-trail row for each change under one correlation id,
    //   3. make the column NOT NULL.

    private const string SystemUserId = "11111111-1111-1111-1111-111111111111";
    private const string CorrelationId = "2fc3efb1-dfcb-4735-bc29-1c311980d443"; // allows the rollback to remove the same audit rows it added on the way up, even if the backfill is run multiple times.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. int -> nullable float (preserves any existing values as doubles).
        migrationBuilder.AlterColumn<double>(
            name: "Rank",
            schema: "Ppm",
            table: "Projects",
            type: "float",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        // 2. Backfill every project's rank, per portfolio, and audit each change. Updates run
        //    first (source of truth); audit rows are written from a pre-captured snapshot.
        migrationBuilder.Sql($@"
            -- Capture each project's old rank and its computed new rank BEFORE mutating, so the update is the
            -- single source of truth and the audit rows are written from what was actually applied.
            SELECT
                p.[Id],
                p.[Rank] AS OldRank,
                CAST(ROW_NUMBER() OVER (PARTITION BY p.[PortfolioId]
                    ORDER BY CASE WHEN p.[Rank] IS NULL THEN 1 ELSE 0 END, p.[Rank], p.[Name], p.[Id]) AS float) * 1000.0 AS NewRank
            INTO #ProjectRankBackfill
            FROM [Ppm].[Projects] p;

            -- Apply the new ranks first.
            UPDATE p
            SET p.[Rank] = b.NewRank
            FROM [Ppm].[Projects] p
            INNER JOIN #ProjectRankBackfill b ON b.[Id] = p.[Id]
            WHERE b.OldRank IS NULL OR b.OldRank <> b.NewRank;

            -- Then record an audit row for each project that actually changed (including null -> value).
            INSERT INTO [Auditing].[AuditTrails]
                ([Id], [UserId], [Type], [SchemaName], [TableName], [DateTime], [OldValues], [NewValues], [AffectedColumns], [PrimaryKey], [CorrelationId])
            SELECT
                NEWID(),
                '{SystemUserId}',
                'Update',
                'Ppm',
                'Project',
                SYSUTCDATETIME(),
                '{{""Rank"":' + CASE WHEN b.OldRank IS NULL THEN 'null' ELSE CONVERT(varchar(32), b.OldRank) END + '}}',
                '{{""Rank"":' + CONVERT(varchar(32), b.NewRank) + '}}',
                '[""Rank""]',
                CONVERT(varchar(450), b.[Id]),
                '{CorrelationId}'
            FROM #ProjectRankBackfill b
            WHERE b.OldRank IS NULL OR b.OldRank <> b.NewRank;

            DROP TABLE #ProjectRankBackfill;
        ");

        // 3. Every project is now ranked, so make the column required.
        migrationBuilder.AlterColumn<double>(
            name: "Rank",
            schema: "Ppm",
            table: "Projects",
            type: "float",
            nullable: false,
            oldClrType: typeof(double),
            oldType: "float",
            oldNullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CurrentScoringModelId",
            schema: "Ppm",
            table: "Projects",
            type: "uniqueidentifier",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CurrentScoringModelId",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.AlterColumn<int>(
            name: "Rank",
            schema: "Ppm",
            table: "Projects",
            type: "int",
            nullable: true,
            oldClrType: typeof(double),
            oldType: "float");

        migrationBuilder.Sql($@"
            IF NOT EXISTS (
                SELECT 1
                FROM [Auditing].[AuditTrails]
                WHERE [CorrelationId] <> '{CorrelationId}'
                  AND [SchemaName] = 'Ppm'
                  AND [TableName] = 'Project'
                  AND [Type] = 'Update'
                  AND [AffectedColumns] LIKE '%""Rank""%'
            )
            BEGIN
                UPDATE p
                SET p.[Rank] = NULL
                FROM [Ppm].[Projects] p
                INNER JOIN [Auditing].[AuditTrails] a
                    ON a.[PrimaryKey] = CONVERT(varchar(450), p.[Id])
                WHERE a.[CorrelationId] = '{CorrelationId}'
                  AND a.[SchemaName] = 'Ppm'
                  AND a.[TableName] = 'Project'
                  AND a.[Type] = 'Update'
                  AND a.[AffectedColumns] = '[""Rank""]';

                DELETE FROM [Auditing].[AuditTrails]
                WHERE [CorrelationId] = '{CorrelationId}'
                  AND [SchemaName] = 'Ppm'
                  AND [TableName] = 'Project'
                  AND [Type] = 'Update'
                  AND [AffectedColumns] = '[""Rank""]';
            END
        ");
    }
}
