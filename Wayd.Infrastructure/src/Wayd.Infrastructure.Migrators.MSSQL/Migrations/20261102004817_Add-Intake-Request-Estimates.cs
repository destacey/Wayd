using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeRequestEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatePosition",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateScaleName",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EstimateScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimateScaleVersionNumber",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateValue",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EstimationScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeDesks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DemandEstimationScales",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Watermarks = table.Column<string>(type: "varchar(1024)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandEstimationScales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemandEstimationScaleVersions",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Values = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandEstimationScaleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandEstimationScaleVersions_DemandEstimationScales_ScaleId",
                        column: x => x.ScaleId,
                        principalSchema: "DemandManagement",
                        principalTable: "DemandEstimationScales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_EstimateScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "EstimateScaleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDesks_EstimationScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeDesks",
                column: "EstimationScaleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandEstimationScaleVersions_ScaleId_Number",
                schema: "DemandManagement",
                table: "DemandEstimationScaleVersions",
                columns: new[] { "ScaleId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeDesks_DemandEstimationScaleVersions_EstimationScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeDesks",
                column: "EstimationScaleVersionId",
                principalSchema: "DemandManagement",
                principalTable: "DemandEstimationScaleVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeRequests_DemandEstimationScaleVersions_EstimateScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "EstimateScaleVersionId",
                principalSchema: "DemandManagement",
                principalTable: "DemandEstimationScaleVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Copies of the scales that predate the replication handler, which keeps them from here on. Planning
            // stores a version's values as the same JSON the copy reads.
            migrationBuilder.Sql(@"
                DECLARE @asOf varchar(64) = CONVERT(varchar(30), SYSUTCDATETIME(), 126) + 'Z';

                INSERT INTO [DemandManagement].[DemandEstimationScales] ([Id], [Key], [Name], [IsActive], [Watermarks])
                SELECT s.[Id], s.[Key], s.[Name], s.[IsActive],
                    JSON_MODIFY(JSON_MODIFY('{}',
                        '$.Name', @asOf),
                        '$.IsActive', @asOf)
                FROM [Planning].[EstimationScales] s;

                INSERT INTO [DemandManagement].[DemandEstimationScaleVersions] ([Id], [ScaleId], [Number], [Values])
                SELECT v.[Id], v.[EstimationScaleId], v.[Number], v.[Values]
                FROM [Planning].[EstimationScaleVersions] v;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeDesks_DemandEstimationScaleVersions_EstimationScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeDesks");

            migrationBuilder.DropForeignKey(
                name: "FK_IntakeRequests_DemandEstimationScaleVersions_EstimateScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropTable(
                name: "DemandEstimationScaleVersions",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "DemandEstimationScales",
                schema: "DemandManagement");

            migrationBuilder.DropIndex(
                name: "IX_IntakeRequests_EstimateScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropIndex(
                name: "IX_IntakeDesks_EstimationScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeDesks");

            migrationBuilder.DropColumn(
                name: "EstimatePosition",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "EstimateScaleName",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "EstimateScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "EstimateScaleVersionNumber",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "EstimateValue",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "EstimationScaleVersionId",
                schema: "DemandManagement",
                table: "IntakeDesks");
        }
    }
}
