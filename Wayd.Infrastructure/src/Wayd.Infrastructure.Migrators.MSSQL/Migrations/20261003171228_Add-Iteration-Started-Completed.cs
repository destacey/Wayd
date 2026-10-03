using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIterationStartedCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Completed",
                schema: "Work",
                table: "Iterations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Started",
                schema: "Work",
                table: "Iterations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Iterations_TeamId_Open",
                schema: "Work",
                table: "Iterations",
                column: "TeamId",
                unique: true,
                filter: "[TeamId] IS NOT NULL AND [Started] IS NOT NULL AND [Completed] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Iterations_TeamId_Open",
                schema: "Work",
                table: "Iterations");

            migrationBuilder.DropColumn(
                name: "Completed",
                schema: "Work",
                table: "Iterations");

            migrationBuilder.DropColumn(
                name: "Started",
                schema: "Work",
                table: "Iterations");
        }
    }
}
