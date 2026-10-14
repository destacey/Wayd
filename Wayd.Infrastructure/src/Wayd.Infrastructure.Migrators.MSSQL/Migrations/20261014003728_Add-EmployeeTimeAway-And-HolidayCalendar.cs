using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeTimeAwayAndHolidayCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HolidayCalendarId",
                schema: "Organization",
                table: "EmployeeCapacities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeTimeAway",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Start = table.Column<DateTime>(type: "date", nullable: false),
                    End = table.Column<DateTime>(type: "date", nullable: false),
                    PercentAway = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTimeAway", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeTimeAway_EmployeeCapacities_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "Organization",
                        principalTable: "EmployeeCapacities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCapacities_HolidayCalendarId",
                schema: "Organization",
                table: "EmployeeCapacities",
                column: "HolidayCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTimeAway_EmployeeId_Start",
                schema: "Organization",
                table: "EmployeeTimeAway",
                columns: new[] { "EmployeeId", "Start" })
                .Annotation("SqlServer:Include", new[] { "End", "PercentAway" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeCapacities_HolidayCalendars_HolidayCalendarId",
                schema: "Organization",
                table: "EmployeeCapacities",
                column: "HolidayCalendarId",
                principalSchema: "Organization",
                principalTable: "HolidayCalendars",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeCapacities_HolidayCalendars_HolidayCalendarId",
                schema: "Organization",
                table: "EmployeeCapacities");

            migrationBuilder.DropTable(
                name: "EmployeeTimeAway",
                schema: "Organization");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCapacities_HolidayCalendarId",
                schema: "Organization",
                table: "EmployeeCapacities");

            migrationBuilder.DropColumn(
                name: "HolidayCalendarId",
                schema: "Organization",
                table: "EmployeeCapacities");
        }
    }
}
