using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AlignIncidentTimelineRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IncidentTimelineLinkEntries_Incidents_IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentTimelineNotes_Incidents_IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineNotes");

            migrationBuilder.DropIndex(
                name: "IX_IncidentTimelineNotes_IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineNotes");

            migrationBuilder.DropIndex(
                name: "IX_IncidentTimelineLinkEntries_IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries");

            // IncidentId1 is the shadow duplicate of IncidentId, not a link target.
            migrationBuilder.DropColumn(
                name: "IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineNotes");

            migrationBuilder.DropColumn(
                name: "IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FromTargetId",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromTargetId",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries");

            migrationBuilder.DropColumn(
                name: "TargetId",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTimelineNotes_IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineNotes",
                column: "IncidentId1");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTimelineLinkEntries_IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries",
                column: "IncidentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentTimelineLinkEntries_Incidents_IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineLinkEntries",
                column: "IncidentId1",
                principalSchema: "ProductManagement",
                principalTable: "Incidents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentTimelineNotes_Incidents_IncidentId1",
                schema: "ProductManagement",
                table: "IncidentTimelineNotes",
                column: "IncidentId1",
                principalSchema: "ProductManagement",
                principalTable: "Incidents",
                principalColumn: "Id");
        }
    }
}
