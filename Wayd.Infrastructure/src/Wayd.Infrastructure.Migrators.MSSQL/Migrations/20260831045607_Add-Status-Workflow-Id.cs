using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusWorkflowId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StatusWorkflowId",
                schema: "Delivery",
                table: "Releases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StatusWorkflowId",
                schema: "Delivery",
                table: "ReleasePackages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StatusWorkflowId",
                schema: "ProductManagement",
                table: "Products",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StatusWorkflowId",
                schema: "Delivery",
                table: "Deployments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // The column defaults to an empty guid, which is the very state that made a workflow switch
            // unresumable — a record reporting no workflow reads as "holds a status from neither" and a
            // re-run fails instead of doing nothing. Existing rows are backfilled from their newest
            // status transition, which has carried the correct workflow id all along.
            //
            // Rows with no transition history keep the empty default: they cannot be resolved from data
            // that does not exist, and a guess would be worse than an obvious blank.
            foreach (var (schema, table, ownerType) in new[]
            {
                ("ProductManagement", "Products", "product.product"),
                ("Delivery", "Releases", "delivery.release"),
                ("Delivery", "ReleasePackages", "delivery.release-package"),
                ("Delivery", "Deployments", "delivery.deployment"),
            })
            {
                migrationBuilder.Sql($"""
                    UPDATE r
                    SET r.[StatusWorkflowId] = t.[WorkflowId]
                    FROM [{schema}].[{table}] r
                    CROSS APPLY (
                        SELECT TOP 1 st.[WorkflowId]
                        FROM [StatusWorkflows].[StatusTransitions] st
                        WHERE st.[OwnerType] = '{ownerType}'
                          AND st.[RecordId] = r.[Id]
                        ORDER BY st.[Sequence] DESC
                    ) t;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusWorkflowId",
                schema: "Delivery",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "StatusWorkflowId",
                schema: "Delivery",
                table: "ReleasePackages");

            migrationBuilder.DropColumn(
                name: "StatusWorkflowId",
                schema: "ProductManagement",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StatusWorkflowId",
                schema: "Delivery",
                table: "Deployments");
        }
    }
}
