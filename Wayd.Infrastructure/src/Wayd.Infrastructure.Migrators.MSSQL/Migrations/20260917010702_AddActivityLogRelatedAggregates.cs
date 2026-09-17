using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLogRelatedAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogRelatedAggregates",
                schema: "App",
                columns: table => new
                {
                    AggregateType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogRelatedAggregates", x => new { x.ActivityLogId, x.AggregateType, x.AggregateId });
                    table.ForeignKey(
                        name: "FK_ActivityLogRelatedAggregates_ActivityLogs_ActivityLogId",
                        column: x => x.ActivityLogId,
                        principalSchema: "App",
                        principalTable: "ActivityLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogRelatedAggregates_AggregateId_AggregateType",
                schema: "App",
                table: "ActivityLogRelatedAggregates",
                columns: new[] { "AggregateId", "AggregateType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogRelatedAggregates",
                schema: "App");
        }
    }
}
