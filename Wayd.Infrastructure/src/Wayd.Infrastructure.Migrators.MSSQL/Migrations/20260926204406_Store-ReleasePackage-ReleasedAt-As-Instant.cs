using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class StoreReleasePackageReleasedAtAsInstant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                schema: "Delivery",
                table: "ReleasePackages",
                type: "datetime2",
                nullable: true);

            // A stored date never held a time of day, so one is chosen: noon UTC falls on the same calendar
            // day in every zone from UTC-11 to UTC+11, so no viewer sees the package move to another day.
            // A package whose real moment matters is corrected through its CorrectDates command.
            migrationBuilder.Sql("""
                UPDATE [Delivery].[ReleasePackages]
                SET [ReleasedAt] = DATEADD(HOUR, 12, CAST([ReleasedDate] AS datetime2))
                WHERE [ReleasedDate] IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ReleasePackages_ReleasedDate",
                schema: "Delivery",
                table: "ReleasePackages");

            migrationBuilder.DropColumn(
                name: "ReleasedDate",
                schema: "Delivery",
                table: "ReleasePackages");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePackages_ReleasedAt",
                schema: "Delivery",
                table: "ReleasePackages",
                column: "ReleasedAt")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Version", "Name", "StatusCategory" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedDate",
                schema: "Delivery",
                table: "ReleasePackages",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Delivery].[ReleasePackages]
                SET [ReleasedDate] = CAST([ReleasedAt] AS date)
                WHERE [ReleasedAt] IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ReleasePackages_ReleasedAt",
                schema: "Delivery",
                table: "ReleasePackages");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                schema: "Delivery",
                table: "ReleasePackages");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePackages_ReleasedDate",
                schema: "Delivery",
                table: "ReleasePackages",
                column: "ReleasedDate")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Version", "Name", "StatusCategory" });
        }
    }
}
