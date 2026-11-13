using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddPokerRoundPublicationAndIntakeRequestEstimateCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublicationAt",
                schema: "Planning",
                table: "PokerRounds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicationByEmployeeId",
                schema: "Planning",
                table: "PokerRounds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationOutcome",
                schema: "Planning",
                table: "PokerRounds",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationRefusalReason",
                schema: "Planning",
                table: "PokerRounds",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateCard",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicationAt",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "PublicationByEmployeeId",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "PublicationOutcome",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "PublicationRefusalReason",
                schema: "Planning",
                table: "PokerRounds");

            migrationBuilder.DropColumn(
                name: "EstimateCard",
                schema: "DemandManagement",
                table: "IntakeRequests");
        }
    }
}
