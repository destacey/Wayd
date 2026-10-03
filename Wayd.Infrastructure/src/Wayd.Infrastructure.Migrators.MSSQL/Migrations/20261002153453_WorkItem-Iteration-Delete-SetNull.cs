using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class WorkItemIterationDeleteSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Iterations_IterationId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Iterations_IterationId",
                schema: "Work",
                table: "WorkItems",
                column: "IterationId",
                principalSchema: "Work",
                principalTable: "Iterations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Iterations_IterationId",
                schema: "Work",
                table: "WorkItems");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Iterations_IterationId",
                schema: "Work",
                table: "WorkItems",
                column: "IterationId",
                principalSchema: "Work",
                principalTable: "Iterations",
                principalColumn: "Id");
        }
    }
}
