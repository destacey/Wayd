using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class DropStrategicInitiativeKpiTables : Migration
    {
        // Runs Move-Strategic-Initiative-Kpis-To-Shared-Model's copy again before dropping the tables it copied from,
        // so a KPI added to them after that migration ran — by a server still on the older code — moves rather than
        // being lost. It copies whole KPIs only: a measurement or checkpoint added then to a KPI already moved is not
        // carried across. A KPI already moved, or removed since it moved, is skipped, so on a database with nothing
        // new the copy does nothing; it stops the migration, as it did then, on a KPI the shared model would refuse.
        //
        // Down recreates the tables and copies every strategic initiative KPI back into them, so the older code finds
        // them again and the move's own Down then removes the shared copies. The old model holds less: a KPI's
        // category, window, at-risk threshold, reading, rolling average and closed status, and a measurement's period
        // and sample size, are not kept. A checkpoint's day becomes midnight of that day in the system time zone, which
        // the move reads back as the same day. What the old model cannot hold at all — a relative target, a value too
        // long for its columns, a measurement nobody is recorded as taking, two measurements at one moment — stops the
        // rollback with the KPIs named, rather than dropping them.

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

            migrationBuilder.Sql(DownSql);
        }

        internal static string DownSql => $$"""
            SET NOCOUNT ON;

            IF NOT EXISTS (SELECT 1 FROM [Kpis].[Kpis] WHERE [SubjectType] = '{{SubjectType}}')
                RETURN;

            {{MoveStrategicInitiativeKpisToSharedModel.SystemZoneSql("Drop-Strategic-Initiative-Kpi-Tables")}}

            CREATE TABLE #Source
            (
                [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                [Order] int NOT NULL
            );

            INSERT INTO #Source
            SELECT k.[Id], ROW_NUMBER() OVER (PARTITION BY k.[SubjectId] ORDER BY k.[Order], k.[Key])
            FROM [Kpis].[Kpis] k
            WHERE k.[SubjectType] = '{{SubjectType}}';

            -- What the old model cannot hold, found before anything is written.
            CREATE TABLE #Problem ([KpiId] uniqueidentifier NOT NULL, [Problem] nvarchar(200) NOT NULL);

            INSERT INTO #Problem
            SELECT k.[Id], N'its target is a change from the baseline'
            FROM [Kpis].[Kpis] k INNER JOIN #Source s ON s.[Id] = k.[Id]
            WHERE k.[TargetKind] <> 'Absolute';

            INSERT INTO #Problem
            SELECT k.[Id], N'its name, description, prefix or suffix is longer than the old model holds'
            FROM [Kpis].[Kpis] k INNER JOIN #Source s ON s.[Id] = k.[Id]
            WHERE LEN(k.[Name]) > 64 OR LEN(k.[Description]) > 512 OR LEN(k.[Prefix]) > 8 OR LEN(k.[Suffix]) > 8;

            INSERT INTO #Problem
            SELECT DISTINCT c.[KpiId], N'a checkpoint label is longer than the old model holds'
            FROM [Kpis].[Checkpoints] c INNER JOIN #Source s ON s.[Id] = c.[KpiId]
            WHERE LEN(c.[Label]) > 16;

            INSERT INTO #Problem
            SELECT DISTINCT m.[KpiId], N'a measurement records no one who took it'
            FROM [Kpis].[Measurements] m INNER JOIN #Source s ON s.[Id] = m.[KpiId]
            WHERE m.[MeasuredById] IS NULL;

            INSERT INTO #Problem
            SELECT DISTINCT m.[KpiId], N'two of its measurements fall at one moment'
            FROM [Kpis].[Measurements] m INNER JOIN #Source s ON s.[Id] = m.[KpiId]
            GROUP BY m.[KpiId], {{Moment("m")}}
            HAVING COUNT(*) > 1;

            IF EXISTS (SELECT 1 FROM #Problem)
            BEGIN
                DECLARE @Problems nvarchar(2000) = (
                    SELECT STRING_AGG(CAST(CONCAT('KPI ', k.[Key], ': ', p.[Problem]) AS nvarchar(max)), '; ')
                    FROM (SELECT TOP (20) * FROM #Problem ORDER BY [KpiId], [Problem]) p
                    INNER JOIN [Kpis].[Kpis] k ON k.[Id] = p.[KpiId]);
                DECLARE @ProblemMessage nvarchar(2048) = CONCAT(
                    'Drop-Strategic-Initiative-Kpi-Tables cannot be rolled back: ', (SELECT COUNT(*) FROM #Problem),
                    ' strategic initiative KPI problem(s) the old model cannot hold: ', @Problems);
                THROW 51000, @ProblemMessage, 1;
            END;

            -- Ordered by the shared key so the old keys keep the order the KPIs were created in.
            INSERT INTO [Ppm].[StrategicInitiativeKpis]
            (
                [Id], [StrategicInitiativeId], [Name], [Description], [StartingValue], [TargetValue], [ActualValue],
                [Prefix], [Suffix], [TargetDirection], [Order],
                [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy]
            )
            SELECT
                k.[Id], k.[SubjectId], k.[Name], k.[Description], k.[BaselineValue], k.[TargetValue], latest.[Value],
                k.[Prefix], k.[Suffix], k.[Direction], s.[Order],
                k.[SystemCreated], k.[SystemCreatedBy], k.[SystemLastModified], k.[SystemLastModifiedBy]
            FROM [Kpis].[Kpis] k
            INNER JOIN #Source s ON s.[Id] = k.[Id]
            OUTER APPLY
            (
                SELECT TOP (1) m.[Value]
                FROM [Kpis].[Measurements] m
                WHERE m.[KpiId] = k.[Id]
                ORDER BY {{Moment("m")}} DESC
            ) latest
            ORDER BY k.[Key];

            INSERT INTO [Ppm].[StrategicInitiativeKpiMeasurements]
            (
                [Id], [KpiId], [ActualValue], [MeasurementDate], [MeasuredById], [Note],
                [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy]
            )
            SELECT
                m.[Id], m.[KpiId], m.[Value], {{Moment("m")}}, m.[MeasuredById], m.[Note],
                m.[SystemCreated], m.[SystemCreatedBy], m.[SystemLastModified], m.[SystemLastModifiedBy]
            FROM [Kpis].[Measurements] m
            INNER JOIN #Source s ON s.[Id] = m.[KpiId];

            INSERT INTO [Ppm].[StrategicInitiativeKpiCheckpoints]
            (
                [Id], [KpiId], [TargetValue], [AtRiskValue], [CheckpointDate], [DateLabel],
                [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy]
            )
            SELECT
                c.[Id], c.[KpiId], c.[PlannedValue], c.[AtRiskValue], {{Midnight("c.[Date]")}}, c.[Label],
                c.[SystemCreated], c.[SystemCreatedBy], c.[SystemLastModified], c.[SystemLastModifiedBy]
            FROM [Kpis].[Checkpoints] c
            INNER JOIN #Source s ON s.[Id] = c.[KpiId];

            DROP TABLE #Problem;
            DROP TABLE #Source;
            DROP TABLE #Zones;
            """;

        // The subject type PpmKpiSubjects.StrategicInitiative declares, frozen here.
        private const string SubjectType = "ppm.strategic-initiative";

        /// <summary>The UTC instant midnight of a date column falls at in the system time zone.</summary>
        private static string Midnight(string column) =>
            $"CAST((CAST({column} AS datetime2) AT TIME ZONE @Windows) AT TIME ZONE 'UTC' AS datetime2)";

        /// <summary>
        /// A measurement's moment: the one it describes when point in time, else midnight of the day it was taken.
        /// </summary>
        private static string Moment(string alias) =>
            $"COALESCE({alias}.[MeasuredAt], {Midnight($"{alias}.[MeasuredOn]")})";
    }
}
