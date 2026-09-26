using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class StoreVersionCutAndReleasedAsInstants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CutAt",
                schema: "Delivery",
                table: "Versions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                schema: "Delivery",
                table: "Versions",
                type: "datetime2",
                nullable: true);

            // A stored date never held a time of day, so one is chosen: noon UTC falls on the same calendar
            // day in every zone from UTC-11 to UTC+11, so no viewer sees the version move to another day.
            // A version whose real moments matter is corrected through its CorrectDates command.
            migrationBuilder.Sql("""
                UPDATE [Delivery].[Versions]
                SET [CutAt] = DATEADD(HOUR, 12, CAST([CutDate] AS datetime2)),
                    [ReleasedAt] = DATEADD(HOUR, 12, CAST([ReleasedDate] AS datetime2))
                WHERE [CutDate] IS NOT NULL OR [ReleasedDate] IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Versions_ProductId_ReleasedDate",
                schema: "Delivery",
                table: "Versions");

            migrationBuilder.DropColumn(
                name: "CutDate",
                schema: "Delivery",
                table: "Versions");

            migrationBuilder.DropColumn(
                name: "ReleasedDate",
                schema: "Delivery",
                table: "Versions");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_ProductId_ReleasedAt",
                schema: "Delivery",
                table: "Versions",
                columns: new[] { "ProductId", "ReleasedAt" })
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Number", "Name", "Sequence", "StatusCategory" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CutDate",
                schema: "Delivery",
                table: "Versions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedDate",
                schema: "Delivery",
                table: "Versions",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Delivery].[Versions]
                SET [CutDate] = CAST([CutAt] AS date),
                    [ReleasedDate] = CAST([ReleasedAt] AS date)
                WHERE [CutAt] IS NOT NULL OR [ReleasedAt] IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Versions_ProductId_ReleasedAt",
                schema: "Delivery",
                table: "Versions");

            migrationBuilder.DropColumn(
                name: "CutAt",
                schema: "Delivery",
                table: "Versions");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                schema: "Delivery",
                table: "Versions");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_ProductId_ReleasedDate",
                schema: "Delivery",
                table: "Versions",
                columns: new[] { "ProductId", "ReleasedDate" })
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Number", "Name", "Sequence", "StatusCategory" });
        }
    }
}