using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddPokerSessionHostAndRoundSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HostId",
                schema: "Planning",
                table: "PokerSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HostKind",
                schema: "Planning",
                table: "PokerSessions",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectDescription",
                schema: "Planning",
                table: "PokerRounds",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectDisplayKey",
                schema: "Planning",
                table: "PokerRounds",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                schema: "Planning",
                table: "PokerRounds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectKind",
                schema: "Planning",
                table: "PokerRounds",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectTitle",
                schema: "Planning",
                table: "PokerRounds",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PokerSessions_HostId",
                schema: "Planning",
                table: "PokerSessions",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_PokerRounds_SubjectId",
                schema: "Planning",
                table: "PokerRounds",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PokerSessions_HostId",
                schema: "Planning",
                table: "PokerSessions");

            migrationBuilder.DropIndex(
                name: "IX_PokerRounds_SubjectId",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "HostId",
                schema: "Planning",
                table: "PokerSessions");

            migrationBuilder.DropColumn(
                name: "HostKind",
                schema: "Planning",
                table: "PokerSessions");

            migrationBuilder.DropColumn(
                name: "SubjectDescription",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "SubjectDisplayKey",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "SubjectKind",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "SubjectTitle",
                schema: "Planning",
                table: "PokerRounds");
        }
    }
}
