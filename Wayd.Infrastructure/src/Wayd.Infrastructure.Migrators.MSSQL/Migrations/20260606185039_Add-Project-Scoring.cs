using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddProjectScoring : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Projects_PortfolioId",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.AddColumn<int>(
            name: "Rank",
            schema: "Ppm",
            table: "Projects",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "CurrentScoreValue",
            schema: "Ppm",
            table: "Projects",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CurrentScoredById",
            schema: "Ppm",
            table: "Projects",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CurrentScoredOn",
            schema: "Ppm",
            table: "Projects",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CurrentScoringModelName",
            schema: "Ppm",
            table: "Projects",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ScoringModelId",
            schema: "Ppm",
            table: "Portfolios",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ProjectScores",
            schema: "Ppm",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_ProjectScores", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectScores_Employees_ScoredById",
                    column: x => x.ScoredById,
                    principalSchema: "Organization",
                    principalTable: "Employees",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectScores_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalSchema: "Ppm",
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectScoreOutputs",
            schema: "Ppm",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_ProjectScoreOutputs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectScoreOutputs_ProjectScores_ProjectScoreId",
                    column: x => x.ProjectScoreId,
                    principalSchema: "Ppm",
                    principalTable: "ProjectScores",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectScoreRatings",
            schema: "Ppm",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                table.PrimaryKey("PK_ProjectScoreRatings", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectScoreRatings_ProjectScores_ProjectScoreId",
                    column: x => x.ProjectScoreId,
                    principalSchema: "Ppm",
                    principalTable: "ProjectScores",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_CurrentScoredById",
            schema: "Ppm",
            table: "Projects",
            column: "CurrentScoredById");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_PortfolioId_Rank",
            schema: "Ppm",
            table: "Projects",
            columns: new[] { "PortfolioId", "Rank" });

        migrationBuilder.CreateIndex(
            name: "IX_Portfolios_ScoringModelId",
            schema: "Ppm",
            table: "Portfolios",
            column: "ScoringModelId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScoreOutputs_ProjectScoreId",
            schema: "Ppm",
            table: "ProjectScoreOutputs",
            column: "ProjectScoreId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScoreRatings_ProjectScoreId",
            schema: "Ppm",
            table: "ProjectScoreRatings",
            column: "ProjectScoreId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScores_ProjectId_Sequence",
            schema: "Ppm",
            table: "ProjectScores",
            columns: new[] { "ProjectId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScores_ScoredById",
            schema: "Ppm",
            table: "ProjectScores",
            column: "ScoredById");

        migrationBuilder.AddForeignKey(
            name: "FK_Portfolios_ScoringModels_ScoringModelId",
            schema: "Ppm",
            table: "Portfolios",
            column: "ScoringModelId",
            principalSchema: "App",
            principalTable: "ScoringModels",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_Projects_Employees_CurrentScoredById",
            schema: "Ppm",
            table: "Projects",
            column: "CurrentScoredById",
            principalSchema: "Organization",
            principalTable: "Employees",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Portfolios_ScoringModels_ScoringModelId",
            schema: "Ppm",
            table: "Portfolios");

        migrationBuilder.DropForeignKey(
            name: "FK_Projects_Employees_CurrentScoredById",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.DropTable(
            name: "ProjectScoreOutputs",
            schema: "Ppm");

        migrationBuilder.DropTable(
            name: "ProjectScoreRatings",
            schema: "Ppm");

        migrationBuilder.DropTable(
            name: "ProjectScores",
            schema: "Ppm");

        migrationBuilder.DropIndex(
            name: "IX_Projects_CurrentScoredById",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.DropIndex(
            name: "IX_Projects_PortfolioId_Rank",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.DropIndex(
            name: "IX_Portfolios_ScoringModelId",
            schema: "Ppm",
            table: "Portfolios");

        migrationBuilder.DropColumn(
            name: "CurrentScoreValue",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "CurrentScoredById",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "CurrentScoredOn",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "CurrentScoringModelName",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "Rank",
            schema: "Ppm",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "ScoringModelId",
            schema: "Ppm",
            table: "Portfolios");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_PortfolioId",
            schema: "Ppm",
            table: "Projects",
            column: "PortfolioId");
    }
}
