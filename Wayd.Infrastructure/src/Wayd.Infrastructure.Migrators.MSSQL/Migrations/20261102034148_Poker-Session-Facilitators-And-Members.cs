using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class PokerSessionFacilitatorsAndMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing session was open to anyone with the View permission.
            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                schema: "Planning",
                table: "PokerSessions",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Public");

            migrationBuilder.CreateTable(
                name: "PokerSessionFacilitators",
                schema: "Planning",
                columns: table => new
                {
                    PokerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokerSessionFacilitators", x => new { x.PokerSessionId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_PokerSessionFacilitators_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PokerSessionFacilitators_PokerSessions_PokerSessionId",
                        column: x => x.PokerSessionId,
                        principalSchema: "Planning",
                        principalTable: "PokerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokerSessionInvitedEmployees",
                schema: "Planning",
                columns: table => new
                {
                    PokerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokerSessionInvitedEmployees", x => new { x.PokerSessionId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_PokerSessionInvitedEmployees_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PokerSessionInvitedEmployees_PokerSessions_PokerSessionId",
                        column: x => x.PokerSessionId,
                        principalSchema: "Planning",
                        principalTable: "PokerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokerSessionInvitedTeams",
                schema: "Planning",
                columns: table => new
                {
                    PokerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokerSessionInvitedTeams", x => new { x.PokerSessionId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_PokerSessionInvitedTeams_PlanningTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Planning",
                        principalTable: "PlanningTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PokerSessionInvitedTeams_PokerSessions_PokerSessionId",
                        column: x => x.PokerSessionId,
                        principalSchema: "Planning",
                        principalTable: "PokerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokerSessionMembers",
                schema: "Planning",
                columns: table => new
                {
                    PokerSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    JoinedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokerSessionMembers", x => new { x.PokerSessionId, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_PokerSessionMembers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PokerSessionMembers_PokerSessions_PokerSessionId",
                        column: x => x.PokerSessionId,
                        principalSchema: "Planning",
                        principalTable: "PokerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PokerSessionFacilitators_EmployeeId",
                schema: "Planning",
                table: "PokerSessionFacilitators",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PokerSessionInvitedEmployees_EmployeeId",
                schema: "Planning",
                table: "PokerSessionInvitedEmployees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PokerSessionInvitedTeams_TeamId",
                schema: "Planning",
                table: "PokerSessionInvitedTeams",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PokerSessionMembers_EmployeeId",
                schema: "Planning",
                table: "PokerSessionMembers",
                column: "EmployeeId");

            // The facilitator was a user; facilitators are employees. A session whose facilitator's account is not
            // linked to an employee is left with none, and only a poker administrator can run it.
            migrationBuilder.Sql("""
                INSERT INTO [Planning].[PokerSessionFacilitators] ([PokerSessionId], [EmployeeId])
                SELECT s.[Id], u.[EmployeeId]
                FROM [Planning].[PokerSessions] s
                INNER JOIN [Identity].[Users] u ON u.[Id] = s.[FacilitatorId]
                INNER JOIN [Organization].[Employees] e ON e.[Id] = u.[EmployeeId];
                """);

            // Everyone who voted took part, so they join as Participants from their first vote. A voter whose account
            // is not linked to an employee cannot be a member and is left out; their votes stay.
            migrationBuilder.Sql("""
                INSERT INTO [Planning].[PokerSessionMembers] ([PokerSessionId], [EmployeeId], [Mode], [JoinedOn], [LeftOn])
                SELECT r.[PokerSessionId], u.[EmployeeId], 'Participant', MIN(v.[SubmittedOn]), NULL
                FROM [Planning].[PokerVotes] v
                INNER JOIN [Planning].[PokerRounds] r ON r.[Id] = v.[PokerRoundId]
                INNER JOIN [Identity].[Users] u ON u.[Id] = v.[ParticipantId]
                INNER JOIN [Organization].[Employees] e ON e.[Id] = u.[EmployeeId]
                GROUP BY r.[PokerSessionId], u.[EmployeeId];
                """);

            migrationBuilder.DropIndex(
                name: "IX_PokerSessions_FacilitatorId",
                schema: "Planning",
                table: "PokerSessions");

            migrationBuilder.DropColumn(
                name: "FacilitatorId",
                schema: "Planning",
                table: "PokerSessions");

            // Running and deleting a session moved from these claims to the session's facilitators, so stored roles
            // would otherwise keep permissions the application no longer defines.
            migrationBuilder.Sql("""
                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = 'permission'
                  AND [ClaimValue] IN (
                      'Permissions.PokerSessions.Update',
                      'Permissions.PokerSessions.Delete');
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Restores one facilitator per session, the account of its first facilitator's employee, falling back to
        /// whoever created it. The removed claims are not restored.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacilitatorId",
                schema: "Planning",
                table: "PokerSessions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE s
                SET [FacilitatorId] = COALESCE(
                    (SELECT TOP 1 u.[Id]
                     FROM [Planning].[PokerSessionFacilitators] f
                     INNER JOIN [Identity].[Users] u ON u.[EmployeeId] = f.[EmployeeId]
                     WHERE f.[PokerSessionId] = s.[Id]
                     ORDER BY f.[EmployeeId], u.[Id]),
                    s.[SystemCreatedBy],
                    '')
                FROM [Planning].[PokerSessions] s;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "FacilitatorId",
                schema: "Planning",
                table: "PokerSessions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PokerSessions_FacilitatorId",
                schema: "Planning",
                table: "PokerSessions",
                column: "FacilitatorId");

            migrationBuilder.DropTable(
                name: "PokerSessionFacilitators",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "PokerSessionInvitedEmployees",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "PokerSessionInvitedTeams",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "PokerSessionMembers",
                schema: "Planning");

            migrationBuilder.DropColumn(
                name: "Visibility",
                schema: "Planning",
                table: "PokerSessions");
        }
    }
}
