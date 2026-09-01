using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusTransitionActorEmployeeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorEmployeeId",
                schema: "StatusWorkflows",
                table: "StatusTransitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusTransitions_ActorEmployeeId",
                schema: "StatusWorkflows",
                table: "StatusTransitions",
                column: "ActorEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_StatusTransitions_Employees_ActorEmployeeId",
                schema: "StatusWorkflows",
                table: "StatusTransitions",
                column: "ActorEmployeeId",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StatusTransitions_Employees_ActorEmployeeId",
                schema: "StatusWorkflows",
                table: "StatusTransitions");

            migrationBuilder.DropIndex(
                name: "IX_StatusTransitions_ActorEmployeeId",
                schema: "StatusWorkflows",
                table: "StatusTransitions");

            migrationBuilder.DropColumn(
                name: "ActorEmployeeId",
                schema: "StatusWorkflows",
                table: "StatusTransitions");
        }
    }
}
