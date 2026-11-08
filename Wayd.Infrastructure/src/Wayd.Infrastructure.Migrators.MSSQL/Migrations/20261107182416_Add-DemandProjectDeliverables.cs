using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandProjectDeliverables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandProjectDeliverables",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    StatusAlias = table.Column<int>(type: "int", nullable: false),
                    Watermarks = table.Column<string>(type: "varchar(1024)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandProjectDeliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandProjectDeliverables_DemandProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "DemandManagement",
                        principalTable: "DemandProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandProjectDeliverables_ProjectId",
                schema: "DemandManagement",
                table: "DemandProjectDeliverables",
                column: "ProjectId");

            // A copy of every existing deliverable whose project has a copy, so outcomes can map deliverables
            // added before Demand Management kept copies. Every group is stamped with the time of the copy, as a
            // resync would stamp it, so an older ProjectDeliverable* event still waiting in the outbox cannot
            // roll the copy back.
            migrationBuilder.Sql(@"
                DECLARE @asOf varchar(64) = CONVERT(varchar(30), SYSUTCDATETIME(), 126) + 'Z';

                INSERT INTO [DemandManagement].[DemandProjectDeliverables] ([Id], [ProjectId], [Name], [StatusCategory], [StatusAlias], [Watermarks])
                SELECT d.[Id], d.[ProjectId], d.[Name], d.[StatusCategory], d.[StatusAliasValue],
                    JSON_MODIFY(JSON_MODIFY('{}',
                        '$.Name', @asOf),
                        '$.Status', @asOf)
                FROM [Ppm].[ProjectDeliverables] d
                INNER JOIN [DemandManagement].[DemandProjects] p ON p.[Id] = d.[ProjectId];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandProjectDeliverables",
                schema: "DemandManagement");
        }
    }
}
