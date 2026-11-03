using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class StoryMapOwnersAndEditors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing map was open to anyone with the View permission and stays so. It stays editable by everyone
            // who can see it, which, with the Update claim gone, widens editing from Update holders to View holders.
            migrationBuilder.AddColumn<string>(
                name: "EditAccess",
                schema: "Planning",
                table: "StoryMaps",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "AnyoneWhoCanView");

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                schema: "Planning",
                table: "StoryMaps",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Public");

            migrationBuilder.CreateTable(
                name: "StoryMapEditors",
                schema: "Planning",
                columns: table => new
                {
                    StoryMapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMapEditors", x => new { x.StoryMapId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_StoryMapEditors_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryMapEditors_StoryMaps_StoryMapId",
                        column: x => x.StoryMapId,
                        principalSchema: "Planning",
                        principalTable: "StoryMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryMapEditorTeams",
                schema: "Planning",
                columns: table => new
                {
                    StoryMapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMapEditorTeams", x => new { x.StoryMapId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_StoryMapEditorTeams_PlanningTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Planning",
                        principalTable: "PlanningTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoryMapEditorTeams_StoryMaps_StoryMapId",
                        column: x => x.StoryMapId,
                        principalSchema: "Planning",
                        principalTable: "StoryMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryMapOwners",
                schema: "Planning",
                columns: table => new
                {
                    StoryMapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMapOwners", x => new { x.StoryMapId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_StoryMapOwners_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryMapOwners_StoryMaps_StoryMapId",
                        column: x => x.StoryMapId,
                        principalSchema: "Planning",
                        principalTable: "StoryMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapEditors_EmployeeId",
                schema: "Planning",
                table: "StoryMapEditors",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapEditorTeams_TeamId",
                schema: "Planning",
                table: "StoryMapEditorTeams",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapOwners_EmployeeId",
                schema: "Planning",
                table: "StoryMapOwners",
                column: "EmployeeId");

            // The owner was a user; owners are employees. A map whose owner's account is not linked to an employee is
            // left with none, and only a Story Maps administrator can manage it; anyone who can view it can still edit
            // its content.
            migrationBuilder.Sql("""
                INSERT INTO [Planning].[StoryMapOwners] ([StoryMapId], [EmployeeId])
                SELECT m.[Id], u.[EmployeeId]
                FROM [Planning].[StoryMaps] m
                INNER JOIN [Identity].[Users] u ON u.[Id] = m.[OwnerId]
                INNER JOIN [Organization].[Employees] e ON e.[Id] = u.[EmployeeId];
                """);

            migrationBuilder.DropIndex(
                name: "IX_StoryMaps_OwnerId_IsDeleted",
                schema: "Planning",
                table: "StoryMaps");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                schema: "Planning",
                table: "StoryMaps");

            // Changing and deleting a map moved from these claims to its owners and edit access, so stored roles would
            // otherwise keep permissions the application no longer defines.
            migrationBuilder.Sql("""
                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = 'permission'
                  AND [ClaimValue] IN (
                      'Permissions.StoryMaps.Update',
                      'Permissions.StoryMaps.Delete');
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Restores one owner per map, the account of one of its owners' employees (owners keep no order), falling back
        /// to whoever created it. Editors, visibility and edit access are dropped, and the removed claims are not
        /// restored.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                schema: "Planning",
                table: "StoryMaps",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE m
                SET [OwnerId] = COALESCE(
                    (SELECT TOP 1 u.[Id]
                     FROM [Planning].[StoryMapOwners] o
                     INNER JOIN [Identity].[Users] u ON u.[EmployeeId] = o.[EmployeeId]
                     WHERE o.[StoryMapId] = m.[Id]
                     ORDER BY o.[EmployeeId], u.[Id]),
                    m.[SystemCreatedBy],
                    '')
                FROM [Planning].[StoryMaps] m;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                schema: "Planning",
                table: "StoryMaps",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoryMaps_OwnerId_IsDeleted",
                schema: "Planning",
                table: "StoryMaps",
                columns: new[] { "OwnerId", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.DropTable(
                name: "StoryMapEditors",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "StoryMapEditorTeams",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "StoryMapOwners",
                schema: "Planning");

            migrationBuilder.DropColumn(
                name: "EditAccess",
                schema: "Planning",
                table: "StoryMaps");

            migrationBuilder.DropColumn(
                name: "Visibility",
                schema: "Planning",
                table: "StoryMaps");
        }
    }
}
