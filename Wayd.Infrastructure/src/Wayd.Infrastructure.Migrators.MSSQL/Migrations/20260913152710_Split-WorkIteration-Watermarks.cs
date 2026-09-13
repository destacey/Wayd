using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class SplitWorkIterationWatermarks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A WorkIteration copy kept one "Record" watermark; it now keeps one per group. Each group starts at
        // the record's watermark, which is when every field was last written. Left as "Record", the groups
        // would read as unset and an older event still in the outbox would roll a copy back. Copies with
        // no watermark ("{}") need nothing.
        migrationBuilder.Sql(@"
                UPDATE [Work].[WorkIterations]
                SET [Watermarks] =
                    JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY('{}',
                        '$.Details', JSON_VALUE([Watermarks], '$.Record')),
                        '$.DateRange', JSON_VALUE([Watermarks], '$.Record')),
                        '$.State', JSON_VALUE([Watermarks], '$.Record')),
                        '$.Team', JSON_VALUE([Watermarks], '$.Record'))
                WHERE JSON_VALUE([Watermarks], '$.Record') IS NOT NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The single watermark takes the newest group, so the old code still skips anything older than
        // the last change it applied.
        migrationBuilder.Sql(@"
                UPDATE w
                SET [Watermarks] = JSON_MODIFY('{}', '$.Record', newest.[Value])
                FROM [Work].[WorkIterations] w
                CROSS APPLY (
                    SELECT TOP (1) g.[Value]
                    FROM (VALUES
                        (JSON_VALUE(w.[Watermarks], '$.Details')),
                        (JSON_VALUE(w.[Watermarks], '$.DateRange')),
                        (JSON_VALUE(w.[Watermarks], '$.State')),
                        (JSON_VALUE(w.[Watermarks], '$.Team'))) g([Value])
                    WHERE g.[Value] IS NOT NULL
                    ORDER BY TRY_CONVERT(datetimeoffset(7), g.[Value], 127) DESC
                ) newest;");
    }
}
