using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RenameProjectPhasesToProjectStages : Migration
    {
        // Scaffolded as DropTable + CreateTable, which would delete every project phase and role
        // assignment. Hand-written as an in-place rename so existing data survives.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_ProjectPhases_ProjectPhaseId",
                schema: "Ppm",
                table: "ProjectTasks");

            migrationBuilder.RenameTable(
                name: "ProjectPhases",
                schema: "Ppm",
                newName: "ProjectStages",
                newSchema: "Ppm");

            migrationBuilder.RenameTable(
                name: "ProjectPhaseAssignments",
                schema: "Ppm",
                newName: "ProjectStageAssignments",
                newSchema: "Ppm");

            // RenameTable does not rename PKs, FKs or indexes — do each explicitly so the schema
            // matches what a fresh CreateTable would produce.
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[PK_ProjectPhases]', N'PK_ProjectStages', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[PK_ProjectPhaseAssignments]', N'PK_ProjectStageAssignments', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[FK_ProjectPhases_Projects_ProjectId]', N'FK_ProjectStages_Projects_ProjectId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[FK_ProjectPhases_ProjectLifecycleStages_ProjectLifecycleStageId]', N'FK_ProjectStages_ProjectLifecycleStages_ProjectLifecycleStageId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[FK_ProjectPhaseAssignments_ProjectPhases_ObjectId]', N'FK_ProjectStageAssignments_ProjectStages_ObjectId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[FK_ProjectPhaseAssignments_Employees_EmployeeId]', N'FK_ProjectStageAssignments_Employees_EmployeeId', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectPhases_ProjectId",
                schema: "Ppm",
                table: "ProjectStages",
                newName: "IX_ProjectStages_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectPhases_ProjectLifecycleStageId",
                schema: "Ppm",
                table: "ProjectStages",
                newName: "IX_ProjectStages_ProjectLifecycleStageId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectPhaseAssignments_EmployeeId",
                schema: "Ppm",
                table: "ProjectStageAssignments",
                newName: "IX_ProjectStageAssignments_EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectPhaseAssignments_ObjectId",
                schema: "Ppm",
                table: "ProjectStageAssignments",
                newName: "IX_ProjectStageAssignments_ObjectId");

            migrationBuilder.RenameColumn(
                name: "ProjectPhaseId",
                schema: "Ppm",
                table: "ProjectTasks",
                newName: "ProjectStageId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectTasks_ProjectPhaseId",
                schema: "Ppm",
                table: "ProjectTasks",
                newName: "IX_ProjectTasks_ProjectStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_ProjectStages_ProjectStageId",
                schema: "Ppm",
                table: "ProjectTasks",
                column: "ProjectStageId",
                principalSchema: "Ppm",
                principalTable: "ProjectStages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_ProjectStages_ProjectStageId",
                schema: "Ppm",
                table: "ProjectTasks");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectTasks_ProjectStageId",
                schema: "Ppm",
                table: "ProjectTasks",
                newName: "IX_ProjectTasks_ProjectPhaseId");

            migrationBuilder.RenameColumn(
                name: "ProjectStageId",
                schema: "Ppm",
                table: "ProjectTasks",
                newName: "ProjectPhaseId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectStageAssignments_ObjectId",
                schema: "Ppm",
                table: "ProjectStageAssignments",
                newName: "IX_ProjectPhaseAssignments_ObjectId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectStageAssignments_EmployeeId",
                schema: "Ppm",
                table: "ProjectStageAssignments",
                newName: "IX_ProjectPhaseAssignments_EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectStages_ProjectLifecycleStageId",
                schema: "Ppm",
                table: "ProjectStages",
                newName: "IX_ProjectPhases_ProjectLifecycleStageId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectStages_ProjectId",
                schema: "Ppm",
                table: "ProjectStages",
                newName: "IX_ProjectPhases_ProjectId");

            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[FK_ProjectStageAssignments_Employees_EmployeeId]', N'FK_ProjectPhaseAssignments_Employees_EmployeeId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[FK_ProjectStageAssignments_ProjectStages_ObjectId]', N'FK_ProjectPhaseAssignments_ProjectPhases_ObjectId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[FK_ProjectStages_ProjectLifecycleStages_ProjectLifecycleStageId]', N'FK_ProjectPhases_ProjectLifecycleStages_ProjectLifecycleStageId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[FK_ProjectStages_Projects_ProjectId]', N'FK_ProjectPhases_Projects_ProjectId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[PK_ProjectStageAssignments]', N'PK_ProjectPhaseAssignments', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'[Ppm].[PK_ProjectStages]', N'PK_ProjectPhases', N'OBJECT';");

            migrationBuilder.RenameTable(
                name: "ProjectStageAssignments",
                schema: "Ppm",
                newName: "ProjectPhaseAssignments",
                newSchema: "Ppm");

            migrationBuilder.RenameTable(
                name: "ProjectStages",
                schema: "Ppm",
                newName: "ProjectPhases",
                newSchema: "Ppm");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_ProjectPhases_ProjectPhaseId",
                schema: "Ppm",
                table: "ProjectTasks",
                column: "ProjectPhaseId",
                principalSchema: "Ppm",
                principalTable: "ProjectPhases",
                principalColumn: "Id");
        }
    }
}
