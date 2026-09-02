using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReleasePackageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Releases_ReleasePackages_PackageId",
                schema: "Delivery",
                table: "Releases");

            migrationBuilder.DropIndex(
                name: "IX_Releases_PackageId",
                schema: "Delivery",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "PackageId",
                schema: "Delivery",
                table: "Releases");

            // Three delivery Delete permissions went with the column's removal: nothing in delivery is
            // deletable, and none of them ever had an endpoint behind it. The seeder only ever adds
            // claims, so without this they would linger on the Admin role matching no permission the
            // application defines.
            migrationBuilder.Sql("""
                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = 'permission'
                  AND [ClaimValue] IN (
                      'Permissions.Releases.Delete',
                      'Permissions.ReleasePackages.Delete',
                      'Permissions.DeploymentEnvironments.Delete');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PackageId",
                schema: "Delivery",
                table: "Releases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Releases_PackageId",
                schema: "Delivery",
                table: "Releases",
                column: "PackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Releases_ReleasePackages_PackageId",
                schema: "Delivery",
                table: "Releases",
                column: "PackageId",
                principalSchema: "Delivery",
                principalTable: "ReleasePackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
