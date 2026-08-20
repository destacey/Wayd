using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RenameProjectLifecyclePhasesToStages : Migration
    {
        // Scaffolded as DropTable + CreateTable, which would delete every lifecycle stage row and orphan
        // the ProjectPhases FK. Hand-written as an in-place rename so existing data survives.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPhases_ProjectLifecyclePhases_ProjectLifecyclePhaseId",
                schema: "Ppm",
                table: "ProjectPhases");

            migrationBuilder.RenameTable(
                name: "ProjectLifecyclePhases",
                schema: "Ppm",
                newName: "ProjectLifecycleStages",
                newSchema: "Ppm");

            // Renaming the table does not rename its primary key or index; do both explicitly so the
            // schema matches what a fresh CreateTable would produce.
            migrationBuilder.RenameIndex(
                name: "IX_ProjectLifecyclePhases_ProjectLifecycleId",
                schema: "Ppm",
                table: "ProjectLifecycleStages",
                newName: "IX_ProjectLifecycleStages_ProjectLifecycleId");

            migrationBuilder.Sql(
                "EXEC sp_rename N'[Ppm].[PK_ProjectLifecyclePhases]', N'PK_ProjectLifecycleStages', N'OBJECT';");

            migrationBuilder.Sql(
                "EXEC sp_rename N'[Ppm].[FK_ProjectLifecyclePhases_ProjectLifecycles_ProjectLifecycleId]', N'FK_ProjectLifecycleStages_ProjectLifecycles_ProjectLifecycleId', N'OBJECT';");

            migrationBuilder.RenameColumn(
                name: "ProjectLifecyclePhaseId",
                schema: "Ppm",
                table: "ProjectPhases",
                newName: "ProjectLifecycleStageId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectPhases_ProjectLifecyclePhaseId",
                schema: "Ppm",
                table: "ProjectPhases",
                newName: "IX_ProjectPhases_ProjectLifecycleStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPhases_ProjectLifecycleStages_ProjectLifecycleStageId",
                schema: "Ppm",
                table: "ProjectPhases",
                column: "ProjectLifecycleStageId",
                principalSchema: "Ppm",
                principalTable: "ProjectLifecycleStages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectPhases_ProjectLifecycleStages_ProjectLifecycleStageId",
                schema: "Ppm",
                table: "ProjectPhases");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectPhases_ProjectLifecycleStageId",
                schema: "Ppm",
                table: "ProjectPhases",
                newName: "IX_ProjectPhases_ProjectLifecyclePhaseId");

            migrationBuilder.RenameColumn(
                name: "ProjectLifecycleStageId",
                schema: "Ppm",
                table: "ProjectPhases",
                newName: "ProjectLifecyclePhaseId");

            migrationBuilder.Sql(
                "EXEC sp_rename N'[Ppm].[FK_ProjectLifecycleStages_ProjectLifecycles_ProjectLifecycleId]', N'FK_ProjectLifecyclePhases_ProjectLifecycles_ProjectLifecycleId', N'OBJECT';");

            migrationBuilder.Sql(
                "EXEC sp_rename N'[Ppm].[PK_ProjectLifecycleStages]', N'PK_ProjectLifecyclePhases', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectLifecycleStages_ProjectLifecycleId",
                schema: "Ppm",
                table: "ProjectLifecycleStages",
                newName: "IX_ProjectLifecyclePhases_ProjectLifecycleId");

            migrationBuilder.RenameTable(
                name: "ProjectLifecycleStages",
                schema: "Ppm",
                newName: "ProjectLifecyclePhases",
                newSchema: "Ppm");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectPhases_ProjectLifecyclePhases_ProjectLifecyclePhaseId",
                schema: "Ppm",
                table: "ProjectPhases",
                column: "ProjectLifecyclePhaseId",
                principalSchema: "Ppm",
                principalTable: "ProjectLifecyclePhases",
                principalColumn: "Id");
        }
    }
}
