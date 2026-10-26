using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class IntakeRequestStatusReasonAndDuplicate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DuplicateOfId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_DuplicateOfId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "DuplicateOfId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeRequests_IntakeRequests_DuplicateOfId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "DuplicateOfId",
                principalSchema: "DemandManagement",
                principalTable: "IntakeRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeRequests_IntakeRequests_DuplicateOfId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropIndex(
                name: "IX_IntakeRequests_DuplicateOfId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "DuplicateOfId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                schema: "DemandManagement",
                table: "IntakeRequests");
        }
    }
}
