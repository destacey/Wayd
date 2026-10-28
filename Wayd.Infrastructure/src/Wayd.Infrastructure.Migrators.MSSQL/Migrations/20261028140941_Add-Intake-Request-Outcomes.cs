using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeRequestOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandProjects",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Watermarks = table.Column<string>(type: "varchar(1024)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeRequestOutcomes",
                schema: "DemandManagement",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AddedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestOutcomes", x => new { x.RequestId, x.TargetId });
                    table.ForeignKey(
                        name: "FK_IntakeRequestOutcomes_Employees_AddedById",
                        column: x => x.AddedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequestOutcomes_IntakeRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestOutcomes_AddedById",
                schema: "DemandManagement",
                table: "IntakeRequestOutcomes",
                column: "AddedById");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestOutcomes_TargetId",
                schema: "DemandManagement",
                table: "IntakeRequestOutcomes",
                column: "TargetId");

            // A copy of every existing project, so outcomes can point at projects created before Demand
            // Management kept copies. Every group is stamped with the time of the copy, as a resync would stamp
            // it, so an older Project* event still waiting in the outbox cannot roll the copy back.
            migrationBuilder.Sql(@"
                DECLARE @asOf varchar(64) = CONVERT(varchar(30), SYSUTCDATETIME(), 126) + 'Z';

                INSERT INTO [DemandManagement].[DemandProjects] ([Id], [Key], [Name], [Watermarks])
                SELECT p.[Id], p.[Key], p.[Name],
                    JSON_MODIFY(JSON_MODIFY('{}',
                        '$.Name', @asOf),
                        '$.Key', @asOf)
                FROM [Ppm].[Projects] p;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandProjects",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeRequestOutcomes",
                schema: "DemandManagement");
        }
    }
}
