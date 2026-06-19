using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class MigrateHealthChecksToPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the new canonical table in the Planning schema.
            migrationBuilder.CreateTable(
                name: "PlanningIntervalObjectiveHealthChecks",
                schema: "Planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanningIntervalObjectiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Deleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ReportedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Expiration = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningIntervalObjectiveHealthChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningIntervalObjectiveHealthChecks_Employees_ReportedById",
                        column: x => x.ReportedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlanningIntervalObjectiveHealthChecks_PlanningIntervalObjectives_PlanningIntervalObjectiveId",
                        column: x => x.PlanningIntervalObjectiveId,
                        principalSchema: "Planning",
                        principalTable: "PlanningIntervalObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningIntervalObjectiveHealthChecks_PlanningIntervalObjectiveId_Expiration_IsDeleted",
                schema: "Planning",
                table: "PlanningIntervalObjectiveHealthChecks",
                columns: new[] { "PlanningIntervalObjectiveId", "Expiration", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningIntervalObjectiveHealthChecks_ReportedById",
                schema: "Planning",
                table: "PlanningIntervalObjectiveHealthChecks",
                column: "ReportedById");

            // 2. Copy historical data from the old Health.HealthChecks table.
            //    Source-of-truth is Health.HealthChecks (it has Note, ReportedBy, full history).
            //    Filter to PlanningPlanningIntervalObjective context only — that's the only consumer today.
            //    Inner-join to PlanningIntervalObjectives so any orphan rows are dropped (no FK in old schema).
            migrationBuilder.Sql(@"
                INSERT INTO [Planning].[PlanningIntervalObjectiveHealthChecks]
                    ([Id], [PlanningIntervalObjectiveId], [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy],
                     [Deleted], [DeletedBy], [IsDeleted], [Status], [ReportedById], [ReportedOn], [Expiration], [Note])
                SELECT
                    hc.[Id],
                    hc.[ObjectId],
                    hc.[SystemCreated],
                    hc.[SystemCreatedBy],
                    hc.[SystemLastModified],
                    hc.[SystemLastModifiedBy],
                    hc.[Deleted],
                    hc.[DeletedBy],
                    hc.[IsDeleted],
                    hc.[Status],
                    hc.[ReportedById],
                    hc.[ReportedOn],
                    hc.[Expiration],
                    hc.[Note]
                FROM [Health].[HealthChecks] AS hc
                INNER JOIN [Planning].[PlanningIntervalObjectives] AS po ON po.[Id] = hc.[ObjectId]
                WHERE hc.[Context] = 'PlanningPlanningIntervalObjective';
            ");

            // 3. Drop the old replicated projection table in Planning.
            migrationBuilder.DropTable(
                name: "PlanningHealthChecks",
                schema: "Planning");

            // 4. Drop the old canonical table in Health.
            migrationBuilder.DropTable(
                name: "HealthChecks",
                schema: "Health");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "Health");

            migrationBuilder.CreateTable(
                name: "HealthChecks",
                schema: "Health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Context = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Deleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Expiration = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthChecks_Employees_ReportedById",
                        column: x => x.ReportedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlanningHealthChecks",
                schema: "Planning",
                columns: table => new
                {
                    ObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Expiration = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningHealthChecks", x => x.ObjectId);
                    table.ForeignKey(
                        name: "FK_PlanningHealthChecks_PlanningIntervalObjectives_ObjectId",
                        column: x => x.ObjectId,
                        principalSchema: "Planning",
                        principalTable: "PlanningIntervalObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HealthChecks_Id",
                schema: "Health",
                table: "HealthChecks",
                column: "Id")
                .Annotation("SqlServer:Include", new[] { "ObjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthChecks_ObjectId",
                schema: "Health",
                table: "HealthChecks",
                column: "ObjectId")
                .Annotation("SqlServer:Include", new[] { "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthChecks_ReportedById",
                schema: "Health",
                table: "HealthChecks",
                column: "ReportedById");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningHealthChecks_ObjectId",
                schema: "Planning",
                table: "PlanningHealthChecks",
                column: "ObjectId");

            migrationBuilder.DropTable(
                name: "PlanningIntervalObjectiveHealthChecks",
                schema: "Planning");
        }
    }
}
