using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IncidentSeveritySets creates this table without Color. A database that already has the
            // column — the earlier color migration, applied before this history was collapsed — must not
            // add it again. A fresh database still receives it here, once.
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = N'ProductManagement'
                      AND t.name = N'IncidentSeverityLevels'
                      AND c.name = N'Color')
                BEGIN
                    ALTER TABLE [ProductManagement].[IncidentSeverityLevels]
                        ADD [Color] nvarchar(9) NOT NULL CONSTRAINT [DF_IncidentSeverityLevels_Color] DEFAULT N'';
                END
                """);

            migrationBuilder.CreateTable(
                name: "Incidents",
                schema: "ProductManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Symptoms = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CommanderEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeverityLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecoveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    StatusAliasValue = table.Column<int>(type: "int", nullable: false),
                    StatusTransitionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.UniqueConstraint("AK_Incidents_Key", x => x.Key);
                    table.ForeignKey(
                        name: "FK_Incidents_IncidentSeverityLevels_SeverityLevelId",
                        column: x => x.SeverityLevelId,
                        principalSchema: "ProductManagement",
                        principalTable: "IncidentSeverityLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentProductLinks",
                schema: "ProductManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentProductLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentProductLinks_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "ProductManagement",
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentProductLinks_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ProductManagement",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentReleaseLinks",
                schema: "ProductManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentReleaseLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentReleaseLinks_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "ProductManagement",
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentReleaseLinks_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalSchema: "Delivery",
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentTimelineLinkEntries",
                schema: "ProductManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTimelineLinkEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentTimelineLinkEntries_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "ProductManagement",
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentTimelineNotes",
                schema: "ProductManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    AuthorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTimelineNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentTimelineNotes_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "ProductManagement",
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentProductLinks_IncidentId_ProductId",
                schema: "ProductManagement",
                table: "IncidentProductLinks",
                columns: new[] { "IncidentId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentProductLinks_ProductId",
                schema: "ProductManagement",
                table: "IncidentProductLinks",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReleaseLinks_IncidentId_ReleaseId",
                schema: "ProductManagement",
                table: "IncidentReleaseLinks",
                columns: new[] { "IncidentId", "ReleaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReleaseLinks_ReleaseId",
                schema: "ProductManagement",
                table: "IncidentReleaseLinks",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_SeverityLevelId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "SeverityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_StatusWorkflowId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "StatusWorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTimelineLinkEntries_IncidentId_RecordedAt",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries",
                columns: new[] { "IncidentId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTimelineNotes_IncidentId_RecordedAt",
                schema: "ProductManagement",
                table: "IncidentTimelineNotes",
                columns: new[] { "IncidentId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentProductLinks",
                schema: "ProductManagement");

            migrationBuilder.DropTable(
                name: "IncidentReleaseLinks",
                schema: "ProductManagement");

            migrationBuilder.DropTable(
                name: "IncidentTimelineLinkEntries",
                schema: "ProductManagement");

            migrationBuilder.DropTable(
                name: "IncidentTimelineNotes",
                schema: "ProductManagement");

            migrationBuilder.DropTable(
                name: "Incidents",
                schema: "ProductManagement");

            migrationBuilder.DropColumn(
                name: "Color",
                schema: "ProductManagement",
                table: "IncidentSeverityLevels");
        }
    }
}
