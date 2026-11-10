using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentReleaseLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentReleaseLinks",
                schema: "ProductManagement");
        }
    }
}
