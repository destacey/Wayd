using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidayCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HolidayCalendarId",
                schema: "Organization",
                table: "TeamOperatingModels",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HolidayCalendars",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayCalendars", x => x.Id);
                    table.UniqueConstraint("AK_HolidayCalendars_Key", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HolidayCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holidays_HolidayCalendars_HolidayCalendarId",
                        column: x => x.HolidayCalendarId,
                        principalSchema: "Organization",
                        principalTable: "HolidayCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamOperatingModels_HolidayCalendarId",
                schema: "Organization",
                table: "TeamOperatingModels",
                column: "HolidayCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_Name",
                schema: "Organization",
                table: "HolidayCalendars",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_HolidayCalendarId_Date",
                schema: "Organization",
                table: "Holidays",
                columns: new[] { "HolidayCalendarId", "Date" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamOperatingModels_HolidayCalendars_HolidayCalendarId",
                schema: "Organization",
                table: "TeamOperatingModels",
                column: "HolidayCalendarId",
                principalSchema: "Organization",
                principalTable: "HolidayCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamOperatingModels_HolidayCalendars_HolidayCalendarId",
                schema: "Organization",
                table: "TeamOperatingModels");

            migrationBuilder.DropTable(
                name: "Holidays",
                schema: "Organization");

            migrationBuilder.DropTable(
                name: "HolidayCalendars",
                schema: "Organization");

            migrationBuilder.DropIndex(
                name: "IX_TeamOperatingModels_HolidayCalendarId",
                schema: "Organization",
                table: "TeamOperatingModels");

            migrationBuilder.DropColumn(
                name: "HolidayCalendarId",
                schema: "Organization",
                table: "TeamOperatingModels");
        }
    }
}
