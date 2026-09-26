using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamOperatingModelTimeZoneAndGrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommitmentGraceDays",
                schema: "Organization",
                table: "TeamOperatingModels",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                schema: "Organization",
                table: "TeamOperatingModels",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "UTC");

            // Team.Create always opens a model, so this only catches a team that predates operating models
            // and was missed by their backfill. Methodology and sizing match that backfill's defaults.
            migrationBuilder.Sql(@"
                INSERT INTO [Organization].[TeamOperatingModels]
                    (Id, TeamId, Start, [End], Methodology, SizingMethod, TimeZone, CommitmentGraceDays, SystemCreated, SystemLastModified)
                SELECT
                    NEWID(),
                    t.Id,
                    t.ActiveDate,
                    NULL,
                    'Kanban',
                    'Count',
                    'UTC',
                    1,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM [Organization].[Teams] t
                WHERE t.IsDeleted = 0
                    AND t.Type = 'Team'
                    AND NOT EXISTS (
                        SELECT 1 FROM [Organization].[TeamOperatingModels] m
                        WHERE m.TeamId = t.Id
                    );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommitmentGraceDays",
                schema: "Organization",
                table: "TeamOperatingModels");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                schema: "Organization",
                table: "TeamOperatingModels");
        }
    }
}
