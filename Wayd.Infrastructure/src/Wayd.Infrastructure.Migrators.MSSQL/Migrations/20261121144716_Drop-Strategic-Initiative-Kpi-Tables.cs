using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class DropStrategicInitiativeKpiTables : Migration
    {
        // Runs Move-Strategic-Initiative-Kpis-To-Shared-Model's copy again before dropping the tables it copied from,
        // so a KPI, checkpoint or measurement written to them after that migration ran — by a server still on the
        // older code, or by the initiative import before it stopped writing them — moves rather than being lost. The
        // copy skips every KPI already moved, so on a database with nothing new it does nothing, and it stops the
        // migration, as it did then, on a KPI the shared model would refuse. Down restores the empty tables only:
        // their rows live on in the shared KPI tables.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MoveStrategicInitiativeKpisToSharedModel.UpSql);

            migrationBuilder.DropTable(
                name: "StrategicInitiativeKpiCheckpoints",
                schema: "Ppm");

            migrationBuilder.DropTable(
                name: "StrategicInitiativeKpiMeasurements",
                schema: "Ppm");

            migrationBuilder.DropTable(
                name: "StrategicInitiativeKpis",
                schema: "Ppm");

            migrationBuilder.DropSequence(
                name: "StrategicInitiativeKpiSequence",
                schema: "Work");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "StrategicInitiativeKpiSequence",
                schema: "Work");

            migrationBuilder.CreateTable(
                name: "StrategicInitiativeKpis",
                schema: "Ppm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualValue = table.Column<double>(type: "float", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Key = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR [Work].[StrategicInitiativeKpiSequence]"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    StartingValue = table.Column<double>(type: "float", nullable: true),
                    StrategicInitiativeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Suffix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TargetDirection = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    TargetValue = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicInitiativeKpis", x => x.Id);
                    table.UniqueConstraint("AK_StrategicInitiativeKpis_Key", x => x.Key);
                    table.ForeignKey(
                        name: "FK_StrategicInitiativeKpis_StrategicInitiatives_StrategicInitiativeId",
                        column: x => x.StrategicInitiativeId,
                        principalSchema: "Ppm",
                        principalTable: "StrategicInitiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategicInitiativeKpiCheckpoints",
                schema: "Ppm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtRiskValue = table.Column<double>(type: "float", nullable: true),
                    CheckpointDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateLabel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    KpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TargetValue = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicInitiativeKpiCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicInitiativeKpiCheckpoints_StrategicInitiativeKpis_KpiId",
                        column: x => x.KpiId,
                        principalSchema: "Ppm",
                        principalTable: "StrategicInitiativeKpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategicInitiativeKpiMeasurements",
                schema: "Ppm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeasuredById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualValue = table.Column<double>(type: "float", nullable: false),
                    KpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeasurementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicInitiativeKpiMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategicInitiativeKpiMeasurements_Employees_MeasuredById",
                        column: x => x.MeasuredById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StrategicInitiativeKpiMeasurements_StrategicInitiativeKpis_KpiId",
                        column: x => x.KpiId,
                        principalSchema: "Ppm",
                        principalTable: "StrategicInitiativeKpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategicInitiativeKpiCheckpoints_KpiId",
                schema: "Ppm",
                table: "StrategicInitiativeKpiCheckpoints",
                column: "KpiId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicInitiativeKpiMeasurements_KpiId",
                schema: "Ppm",
                table: "StrategicInitiativeKpiMeasurements",
                column: "KpiId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicInitiativeKpiMeasurements_MeasuredById",
                schema: "Ppm",
                table: "StrategicInitiativeKpiMeasurements",
                column: "MeasuredById");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicInitiativeKpis_StrategicInitiativeId",
                schema: "Ppm",
                table: "StrategicInitiativeKpis",
                column: "StrategicInitiativeId");
        }
    }
}
