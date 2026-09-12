using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddActivityLogOrdinal : Migration
{
    // Gives the activity log a total order. The events of one command land microseconds apart at best, and on
    // the same Timestamp where the caller reads the clock once for a whole batch — an import or a sync — so
    // reads that sorted on it alone left rows tied, and Skip/Take over a tie the database is free to break
    // differently per query could return an entry on two pages or on neither. Reads now sort by Timestamp,
    // then Ordinal, then Id — Id is unique, so the three cannot tie.
    //
    // Existing rows keep the column default of 0 and so stay tied with each other, falling through to Id.
    // Their raise order is not recoverable — losing it is the fact this column exists to stop — and a
    // backfill could only have invented one. What they gain is the part that was actually broken: an
    // answer that is the same on every read, so paging over them no longer repeats or skips an entry.
    //
    // The record index is also rebuilt with AggregateId ahead of AggregateType, which a read filters on
    // always rather than optionally.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ActivityLogs_AggregateType_AggregateId_Timestamp",
            schema: "App",
            table: "ActivityLogs");

        migrationBuilder.DropIndex(
            name: "IX_ActivityLogs_Timestamp",
            schema: "App",
            table: "ActivityLogs");

        migrationBuilder.DropIndex(
            name: "IX_ActivityLogs_UserId_Timestamp",
            schema: "App",
            table: "ActivityLogs");

        migrationBuilder.AddColumn<int>(
            name: "Ordinal",
            schema: "App",
            table: "ActivityLogs",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_AggregateId_AggregateType_Timestamp_Ordinal",
            schema: "App",
            table: "ActivityLogs",
            columns: new[] { "AggregateId", "AggregateType", "Timestamp", "Ordinal" });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_Timestamp_Ordinal",
            schema: "App",
            table: "ActivityLogs",
            columns: new[] { "Timestamp", "Ordinal" });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_UserId_Timestamp_Ordinal",
            schema: "App",
            table: "ActivityLogs",
            columns: new[] { "UserId", "Timestamp", "Ordinal" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ActivityLogs_AggregateId_AggregateType_Timestamp_Ordinal",
            schema: "App",
            table: "ActivityLogs");

        migrationBuilder.DropIndex(
            name: "IX_ActivityLogs_Timestamp_Ordinal",
            schema: "App",
            table: "ActivityLogs");

        migrationBuilder.DropIndex(
            name: "IX_ActivityLogs_UserId_Timestamp_Ordinal",
            schema: "App",
            table: "ActivityLogs");

        migrationBuilder.DropColumn(
            name: "Ordinal",
            schema: "App",
            table: "ActivityLogs");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_AggregateType_AggregateId_Timestamp",
            schema: "App",
            table: "ActivityLogs",
            columns: new[] { "AggregateType", "AggregateId", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_Timestamp",
            schema: "App",
            table: "ActivityLogs",
            column: "Timestamp");

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_UserId_Timestamp",
            schema: "App",
            table: "ActivityLogs",
            columns: new[] { "UserId", "Timestamp" });
    }
}
