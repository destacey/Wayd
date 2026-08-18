using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddProjectStatusTransitionSequence : Migration
{
    // Gives every status history row a monotonic per-project Sequence, and every project a count of the
    // transitions it has recorded, which is where the next row's sequence comes from.
    //
    // The steps must stay in this order: the columns have to exist before they can be filled, and the
    // unique index has to come last, because until the backfill runs every row holds the default 0 and
    // most projects have several rows.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Sequence",
            schema: "Ppm",
            table: "ProjectStatusHistory",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "StatusTransitionCount",
            schema: "Ppm",
            table: "Projects",
            type: "int",
            nullable: false,
            defaultValue: 0);

        // Existing history is forward-only, so chronological order is its true order.
        //
        // ChangedOn alone does not separate the rows — imports and the reconstruction migration both
        // write several rows at one instant. The origin row is pinned first by its null FromStatus, and
        // Id breaks any remaining tie so re-running produces the same numbering.
        migrationBuilder.Sql(@"
                WITH Ordered AS
                (
                    SELECT
                        h.[Id],
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY h.[ProjectId]
                            ORDER BY
                                CASE WHEN h.[FromStatus] IS NULL THEN 0 ELSE 1 END,
                                h.[ChangedOn],
                                h.[Id]
                        ) AS Seq
                    FROM [Ppm].[ProjectStatusHistory] h
                )
                UPDATE h
                SET h.[Sequence] = o.Seq
                FROM [Ppm].[ProjectStatusHistory] h
                INNER JOIN Ordered o ON o.[Id] = h.[Id];
            ");

        // The count must equal the project's existing row count, or the next transition hands out a
        // sequence already taken. Projects with no history keep the default 0, so their first transition
        // takes sequence 1.
        migrationBuilder.Sql(@"
                UPDATE p
                SET p.[StatusTransitionCount] = h.RowCountForProject
                FROM [Ppm].[Projects] p
                INNER JOIN
                (
                    SELECT [ProjectId], COUNT(*) AS RowCountForProject
                    FROM [Ppm].[ProjectStatusHistory]
                    GROUP BY [ProjectId]
                ) h ON h.[ProjectId] = p.[Id];
            ");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectStatusHistory_ProjectId_Sequence",
            schema: "Ppm",
            table: "ProjectStatusHistory",
            columns: new[] { "ProjectId", "Sequence" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProjectStatusHistory_ProjectId_Sequence",
            schema: "Ppm",
            table: "ProjectStatusHistory");

        migrationBuilder.DropColumn(
            name: "Sequence",
            schema: "Ppm",
            table: "ProjectStatusHistory");

        migrationBuilder.DropColumn(
            name: "StatusTransitionCount",
            schema: "Ppm",
            table: "Projects");
    }
}
