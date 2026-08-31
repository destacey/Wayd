using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddStatusWorkflowIdIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Releases_StatusWorkflowId",
            schema: "Delivery",
            table: "Releases",
            column: "StatusWorkflowId");

        migrationBuilder.CreateIndex(
            name: "IX_ReleasePackages_StatusWorkflowId",
            schema: "Delivery",
            table: "ReleasePackages",
            column: "StatusWorkflowId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_StatusWorkflowId",
            schema: "ProductManagement",
            table: "Products",
            column: "StatusWorkflowId");

        migrationBuilder.CreateIndex(
            name: "IX_Deployments_StatusWorkflowId",
            schema: "Delivery",
            table: "Deployments",
            column: "StatusWorkflowId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Releases_StatusWorkflowId",
            schema: "Delivery",
            table: "Releases");

        migrationBuilder.DropIndex(
            name: "IX_ReleasePackages_StatusWorkflowId",
            schema: "Delivery",
            table: "ReleasePackages");

        migrationBuilder.DropIndex(
            name: "IX_Products_StatusWorkflowId",
            schema: "ProductManagement",
            table: "Products");

        migrationBuilder.DropIndex(
            name: "IX_Deployments_StatusWorkflowId",
            schema: "Delivery",
            table: "Deployments");
    }
}
