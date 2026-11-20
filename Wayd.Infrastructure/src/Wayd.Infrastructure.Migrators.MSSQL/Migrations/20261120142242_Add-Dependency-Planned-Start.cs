using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddDependencyPlannedStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SourcePlannedStart",
                schema: "Work",
                table: "WorkItemLinks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetPlannedStart",
                schema: "Work",
                table: "WorkItemLinks",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourcePlannedStart",
                schema: "Work",
                table: "WorkItemLinks");

            migrationBuilder.DropColumn(
                name: "TargetPlannedStart",
                schema: "Work",
                table: "WorkItemLinks");
        }
    }
}
