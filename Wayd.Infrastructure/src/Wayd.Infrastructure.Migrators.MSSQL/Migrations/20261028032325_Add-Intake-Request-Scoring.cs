using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeRequestScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentScoreValue",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentScoredById",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentScoredOn",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentScoringModelId",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentScoringModelName",
                schema: "DemandManagement",
                table: "IntakeRequests",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScoringModelId",
                schema: "DemandManagement",
                table: "IntakeDesks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntakeRequestScores",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoringModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoringModelKey = table.Column<int>(type: "int", nullable: false),
                    ScoringModelName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PrimaryValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ScoredOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScoredById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeRequestScores_Employees_ScoredById",
                        column: x => x.ScoredById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequestScores_IntakeRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeRequestScoreOutputs",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestScoreOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeRequestScoreOutputs_IntakeRequestScores_ScoreId",
                        column: x => x.ScoreId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequestScores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeRequestScoreRatings",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriterionName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CriterionToken = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    RatingValue = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    RatingLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RatingLevelLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestScoreRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeRequestScoreRatings_IntakeRequestScores_ScoreId",
                        column: x => x.ScoreId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequestScores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequests_CurrentScoredById",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "CurrentScoredById");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDesks_ScoringModelId",
                schema: "DemandManagement",
                table: "IntakeDesks",
                column: "ScoringModelId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestScoreOutputs_ScoreId",
                schema: "DemandManagement",
                table: "IntakeRequestScoreOutputs",
                column: "ScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestScoreRatings_ScoreId",
                schema: "DemandManagement",
                table: "IntakeRequestScoreRatings",
                column: "ScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestScores_RequestId_Sequence",
                schema: "DemandManagement",
                table: "IntakeRequestScores",
                columns: new[] { "RequestId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestScores_ScoredById",
                schema: "DemandManagement",
                table: "IntakeRequestScores",
                column: "ScoredById");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeDesks_ScoringModels_ScoringModelId",
                schema: "DemandManagement",
                table: "IntakeDesks",
                column: "ScoringModelId",
                principalSchema: "App",
                principalTable: "ScoringModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeRequests_Employees_CurrentScoredById",
                schema: "DemandManagement",
                table: "IntakeRequests",
                column: "CurrentScoredById",
                principalSchema: "Organization",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntakeDesks_ScoringModels_ScoringModelId",
                schema: "DemandManagement",
                table: "IntakeDesks");

            migrationBuilder.DropForeignKey(
                name: "FK_IntakeRequests_Employees_CurrentScoredById",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropTable(
                name: "IntakeRequestScoreOutputs",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeRequestScoreRatings",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeRequestScores",
                schema: "DemandManagement");

            migrationBuilder.DropIndex(
                name: "IX_IntakeRequests_CurrentScoredById",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropIndex(
                name: "IX_IntakeDesks_ScoringModelId",
                schema: "DemandManagement",
                table: "IntakeDesks");

            migrationBuilder.DropColumn(
                name: "CurrentScoreValue",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "CurrentScoredById",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "CurrentScoredOn",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "CurrentScoringModelId",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "CurrentScoringModelName",
                schema: "DemandManagement",
                table: "IntakeRequests");

            migrationBuilder.DropColumn(
                name: "ScoringModelId",
                schema: "DemandManagement",
                table: "IntakeDesks");
        }
    }
}
