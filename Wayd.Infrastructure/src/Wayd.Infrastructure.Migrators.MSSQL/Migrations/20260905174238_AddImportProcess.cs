using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddImportProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Imports");

            migrationBuilder.CreateTable(
                name: "ImportProcesses",
                schema: "Imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubmissionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastAttemptCorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastProgressOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalRowCount = table.Column<int>(type: "int", nullable: false),
                    SucceededRowCount = table.Column<int>(type: "int", nullable: false),
                    FailedRowCount = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProcesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportProcessRows",
                schema: "Imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Warning = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AttemptedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProcessRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProcessRows_ImportProcesses_ImportProcessId",
                        column: x => x.ImportProcessId,
                        principalSchema: "Imports",
                        principalTable: "ImportProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProcesses_Status_CompletedOn",
                schema: "Imports",
                table: "ImportProcesses",
                columns: new[] { "Status", "CompletedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProcesses_Status_LastProgressOn",
                schema: "Imports",
                table: "ImportProcesses",
                columns: new[] { "Status", "LastProgressOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProcesses_SubmissionGroupId",
                schema: "Imports",
                table: "ImportProcesses",
                column: "SubmissionGroupId",
                filter: "[SubmissionGroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProcesses_SubmittedOn",
                schema: "Imports",
                table: "ImportProcesses",
                column: "SubmittedOn");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProcessRows_ImportProcessId_ImportId",
                schema: "Imports",
                table: "ImportProcessRows",
                columns: new[] { "ImportProcessId", "ImportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportProcessRows_ImportProcessId_Status",
                schema: "Imports",
                table: "ImportProcessRows",
                columns: new[] { "ImportProcessId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProcessRows",
                schema: "Imports");

            migrationBuilder.DropTable(
                name: "ImportProcesses",
                schema: "Imports");
        }
    }
}
