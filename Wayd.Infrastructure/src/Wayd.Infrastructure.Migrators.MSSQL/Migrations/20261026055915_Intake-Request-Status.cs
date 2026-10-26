using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class IntakeRequestStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusAliasValue",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StatusCategory",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "StatusId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "StatusName",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StatusTransitionCount",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "StatusWorkflowId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_StatusWorkflowId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "StatusWorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntakeRequests_StatusWorkflowId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "StatusAliasValue",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "StatusCategory",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "StatusId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "StatusName",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "StatusTransitionCount",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "StatusWorkflowId",
                schema: "DemandManagement",
                table: "IntakeRequests");
        }
    }
}
