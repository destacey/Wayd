using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class DropPersonalAccessTokenEmployeeId : Migration
    {
        // PersonalAccessTokens.EmployeeId was a copy of the owner's employee link, frozen when the token
        // was created. It was never refreshed, so a token minted before its owner was linked stayed
        // employee-less for its whole lifetime and a token outlived any later re-link. The authentication
        // handler now reads the owner's current link (Identity.Users.EmployeeId) instead, which left this
        // column written-but-never-read — a field that still looks authoritative and invites exactly the
        // staleness bug it caused. Removing it makes the user record the single source of the link.
        //
        // Down restores the column, its index, and the FK, but NOT the values: the old snapshots are not
        // recoverable, and re-deriving them from the users' current links would fabricate history rather
        // than restore it. Rolling back therefore yields an all-NULL column, which is harmless because no
        // code reads it.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonalAccessTokens_Employees_EmployeeId",
                schema: "Identity",
                table: "PersonalAccessTokens");

            migrationBuilder.DropIndex(
                name: "IX_PersonalAccessTokens_EmployeeId",
                schema: "Identity",
                table: "PersonalAccessTokens");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                schema: "Identity",
                table: "PersonalAccessTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                schema: "Identity",
                table: "PersonalAccessTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAccessTokens_EmployeeId",
                schema: "Identity",
                table: "PersonalAccessTokens",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalAccessTokens_Employees_EmployeeId",
                schema: "Identity",
                table: "PersonalAccessTokens",
                column: "EmployeeId",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id");
        }
    }
}
