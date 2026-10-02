using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningSprints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanningIntervalIterationSprints_Iterations_SprintId",
                schema: "Planning",
                table: "PlanningIntervalIterationSprints");

            migrationBuilder.CreateTable(
                name: "PlanningSprints",
                schema: "Planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Watermarks = table.Column<string>(type: "varchar(1024)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    End = table.Column<DateTime>(type: "date", nullable: true),
                    Start = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningSprints", x => x.Id);
                    table.UniqueConstraint("AK_PlanningSprints_Key", x => x.Key);
                    table.ForeignKey(
                        name: "FK_PlanningSprints_PlanningTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "Planning",
                        principalTable: "PlanningTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningSprints_TeamId",
                schema: "Planning",
                table: "PlanningSprints",
                column: "TeamId")
                .Annotation("SqlServer:Include", new[] { "Key", "Name", "Type", "State" });

            // Every group is stamped with the time of the copy, as a resync would stamp it. Left unset, an older
            // Iteration* event still waiting in the outbox would roll the copy back past the source.
            migrationBuilder.Sql(@"
                DECLARE @asOf varchar(64) = CONVERT(varchar(30), SYSUTCDATETIME(), 126) + 'Z';

                INSERT INTO [Planning].[PlanningSprints] ([Id], [Key], [Name], [Type], [State], [TeamId], [Start], [End], [Watermarks])
                SELECT i.[Id], i.[Key], i.[Name], i.[Type], i.[State], i.[TeamId], i.[Start], i.[End],
                    JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY('{}',
                        '$.Details', @asOf),
                        '$.DateRange', @asOf),
                        '$.State', @asOf),
                        '$.Team', @asOf)
                FROM [Planning].[Iterations] i;");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanningIntervalIterationSprints_PlanningSprints_SprintId",
                schema: "Planning",
                table: "PlanningIntervalIterationSprints",
                column: "SprintId",
                principalSchema: "Planning",
                principalTable: "PlanningSprints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanningIntervalIterationSprints_PlanningSprints_SprintId",
                schema: "Planning",
                table: "PlanningIntervalIterationSprints");

            migrationBuilder.DropTable(
                name: "PlanningSprints",
                schema: "Planning");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanningIntervalIterationSprints_Iterations_SprintId",
                schema: "Planning",
                table: "PlanningIntervalIterationSprints",
                column: "SprintId",
                principalSchema: "Planning",
                principalTable: "Iterations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
