using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Renamed in place: EF scaffolds a table rename as a drop and a create, which would delete every roadmap's
    /// owners. RenameTable leaves the key and foreign key names behind, so they are renamed too.
    /// </remarks>
    public partial class RenameRoadmapManagersToOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "RoadmapManagers",
                schema: "Planning",
                newName: "RoadmapOwners",
                newSchema: "Planning");

            migrationBuilder.RenameColumn(
                name: "ManagerId",
                schema: "Planning",
                table: "RoadmapOwners",
                newName: "EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_RoadmapManagers_ManagerId",
                schema: "Planning",
                table: "RoadmapOwners",
                newName: "IX_RoadmapOwners_EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_RoadmapManagers_RoadmapId",
                schema: "Planning",
                table: "RoadmapOwners",
                newName: "IX_RoadmapOwners_RoadmapId");

            migrationBuilder.Sql("EXEC sp_rename N'[Planning].[PK_RoadmapManagers]', N'PK_RoadmapOwners', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Planning].[FK_RoadmapManagers_Employees_ManagerId]', N'FK_RoadmapOwners_Employees_EmployeeId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Planning].[FK_RoadmapManagers_Roadmaps_RoadmapId]', N'FK_RoadmapOwners_Roadmaps_RoadmapId', N'OBJECT';");

            // Changing and deleting a roadmap moved from these claims to its owners, so stored roles would otherwise
            // keep permissions the application no longer defines.
            migrationBuilder.Sql("""
                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = 'permission'
                  AND [ClaimValue] IN (
                      'Permissions.Roadmaps.Update',
                      'Permissions.Roadmaps.Delete');
                """);
        }

        /// <inheritdoc />
        /// <remarks>The removed claims are not restored.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC sp_rename N'[Planning].[FK_RoadmapOwners_Roadmaps_RoadmapId]', N'FK_RoadmapManagers_Roadmaps_RoadmapId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Planning].[FK_RoadmapOwners_Employees_EmployeeId]', N'FK_RoadmapManagers_Employees_ManagerId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Planning].[PK_RoadmapOwners]', N'PK_RoadmapManagers', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_RoadmapOwners_RoadmapId",
                schema: "Planning",
                table: "RoadmapOwners",
                newName: "IX_RoadmapManagers_RoadmapId");

            migrationBuilder.RenameIndex(
                name: "IX_RoadmapOwners_EmployeeId",
                schema: "Planning",
                table: "RoadmapOwners",
                newName: "IX_RoadmapManagers_ManagerId");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                schema: "Planning",
                table: "RoadmapOwners",
                newName: "ManagerId");

            migrationBuilder.RenameTable(
                name: "RoadmapOwners",
                schema: "Planning",
                newName: "RoadmapManagers",
                newSchema: "Planning");
        }
    }
}
