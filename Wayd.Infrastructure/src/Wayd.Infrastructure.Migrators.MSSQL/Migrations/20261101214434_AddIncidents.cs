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
            migrationBuilder.CreateTable(
                name: "Incidents",
                schema: "ProductManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
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
                name: "IX_Incidents_SeverityLevelId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "SeverityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_StatusWorkflowId",
                schema: "ProductManagement",
                table: "Incidents",
                column: "StatusWorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentProductLinks",
                schema: "ProductManagement");

            migrationBuilder.DropTable(
                name: "Incidents",
                schema: "ProductManagement");
        }
    }
}
