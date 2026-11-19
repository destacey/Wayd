using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiMeasurementWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Measurements_KpiId_PeriodEnd",
                schema: "Kpis",
                table: "Measurements");

            migrationBuilder.DropIndex(
                name: "IX_Measurements_KpiId_PeriodStart",
                schema: "Kpis",
                table: "Measurements");

            migrationBuilder.AddColumn<DateTime>(
                name: "MeasuredAt",
                schema: "Kpis",
                table: "Measurements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RollingAverageOf",
                schema: "Kpis",
                table: "Kpis",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WindowAnchorDate",
                schema: "Kpis",
                table: "Kpis",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowKind",
                schema: "Kpis",
                table: "Kpis",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                // Every KPI before windows existed took a period per measurement.
                defaultValue: "PerPeriod");

            migrationBuilder.AddColumn<int>(
                name: "WindowRollingCount",
                schema: "Kpis",
                table: "Kpis",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowRollingUnit",
                schema: "Kpis",
                table: "Kpis",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_KpiId_PeriodEnd_MeasuredAt",
                schema: "Kpis",
                table: "Measurements",
                columns: new[] { "KpiId", "PeriodEnd", "MeasuredAt" },
                unique: true,
                descending: new[] { false, true, true })
                .Annotation("SqlServer:Include", new[] { "Value" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Measurements_KpiId_PeriodEnd_MeasuredAt",
                schema: "Kpis",
                table: "Measurements");

            migrationBuilder.DropColumn(
                name: "MeasuredAt",
                schema: "Kpis",
                table: "Measurements");

            migrationBuilder.DropColumn(
                name: "RollingAverageOf",
                schema: "Kpis",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "WindowAnchorDate",
                schema: "Kpis",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "WindowKind",
                schema: "Kpis",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "WindowRollingCount",
                schema: "Kpis",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "WindowRollingUnit",
                schema: "Kpis",
                table: "Kpis");

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_KpiId_PeriodEnd",
                schema: "Kpis",
                table: "Measurements",
                columns: new[] { "KpiId", "PeriodEnd" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_Measurements_KpiId_PeriodStart",
                schema: "Kpis",
                table: "Measurements",
                columns: new[] { "KpiId", "PeriodStart" },
                unique: true);
        }
    }
}
