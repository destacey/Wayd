using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class MoveIterationsToWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Iterations_PlanningTeams_TeamId",
                schema: "Planning",
                table: "Iterations");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkIterations_IterationId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkIterations",
                schema: "Work");

            migrationBuilder.RenameTable(
                name: "Iterations",
                schema: "Planning",
                newName: "Iterations",
                newSchema: "Work");

            migrationBuilder.RenameTable(
                name: "IterationExternalMetadata",
                schema: "Planning",
                newName: "IterationExternalMetadata",
                newSchema: "Work");

            // The sprint's team now points at the Work copy of the team rather than the Planning one. Both copies
            // hold every Organization team, so a sprint whose team has no Work copy means the copies drifted.
            // Clearing its team would be a set-based write to an evented aggregate, so the migration stops and
            // says which team instead; running the Teams sync job repairs it.
            migrationBuilder.Sql(@"
                DECLARE @missingTeamId uniqueidentifier = (
                    SELECT TOP (1) i.[TeamId]
                    FROM [Work].[Iterations] i
                    WHERE i.[TeamId] IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM [Work].[WorkTeams] t WHERE t.[Id] = i.[TeamId]));
                IF @missingTeamId IS NOT NULL
                BEGIN
                    DECLARE @message nvarchar(400) = CONCAT(N'Team ', @missingTeamId, N' owns a sprint but has no Work copy. Run the Teams sync job, then start the application again.');
                    THROW 50001, @message, 1;
                END");

            // A work item could point at a Work copy of a sprint whose source was already deleted, because the
            // copy's foreign key refused its removal. The sprint is gone, so the reference goes with it.
            migrationBuilder.Sql(@"
                UPDATE w
                SET w.[IterationId] = NULL
                FROM [Work].[WorkItems] w
                WHERE w.[IterationId] IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM [Work].[Iterations] i WHERE i.[Id] = w.[IterationId]);");

            migrationBuilder.AddForeignKey(
                name: "FK_Iterations_WorkTeams_TeamId",
                schema: "Work",
                table: "Iterations",
                column: "TeamId",
                principalSchema: "Work",
                principalTable: "WorkTeams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Iterations_IterationId",
                schema: "Work",
                table: "WorkItems",
                column: "IterationId",
                principalSchema: "Work",
                principalTable: "Iterations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Iterations_WorkTeams_TeamId",
                schema: "Work",
                table: "Iterations");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Iterations_IterationId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.RenameTable(
                name: "Iterations",
                schema: "Work",
                newName: "Iterations",
                newSchema: "Planning");

            migrationBuilder.RenameTable(
                name: "IterationExternalMetadata",
                schema: "Work",
                newName: "IterationExternalMetadata",
                newSchema: "Planning");

            migrationBuilder.CreateTable(
                name: "WorkIterations",
                schema: "Work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Key = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    State = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Watermarks = table.Column<string>(type: "varchar(1024)", nullable: false),
                    End = table.Column<DateTime>(type: "date", nullable: true),
                    Start = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkIterations", x => x.Id);
                    table.UniqueConstraint("AK_WorkIterations_Key", x => x.Key);
                    table.ForeignKey(
                        name: "FK_WorkIterations_WorkTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Work",
                        principalTable: "WorkTeams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkIterations_TeamId",
                schema: "Work",
                table: "WorkIterations",
                column: "TeamId");

            // The copy is rebuilt from the sprints, every group stamped now, as a resync would stamp it.
            migrationBuilder.Sql(@"
                DECLARE @asOf varchar(64) = CONVERT(varchar(30), SYSUTCDATETIME(), 126) + 'Z';

                INSERT INTO [Work].[WorkIterations] ([Id], [Key], [Name], [Type], [State], [TeamId], [Start], [End], [Watermarks])
                SELECT i.[Id], i.[Key], i.[Name], i.[Type], i.[State], i.[TeamId], i.[Start], i.[End],
                    JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY('{}',
                        '$.Details', @asOf),
                        '$.DateRange', @asOf),
                        '$.State', @asOf),
                        '$.Team', @asOf)
                FROM [Planning].[Iterations] i;");

            migrationBuilder.AddForeignKey(
                name: "FK_Iterations_PlanningTeams_TeamId",
                schema: "Planning",
                table: "Iterations",
                column: "TeamId",
                principalSchema: "Planning",
                principalTable: "PlanningTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkIterations_IterationId",
                schema: "Work",
                table: "WorkItems",
                column: "IterationId",
                principalSchema: "Work",
                principalTable: "WorkIterations",
                principalColumn: "Id");
        }
    }
}
