using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "App");

            migrationBuilder.CreateTable(
                name: "ScoringModels",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    State = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringModels", x => x.Id);
                    table.UniqueConstraint("AK_ScoringModels_Key", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ScoringModelOutputs",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoringModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Token = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Formula = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringModelOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringModelOutputs_ScoringModels_ScoringModelId",
                        column: x => x.ScoringModelId,
                        principalSchema: "App",
                        principalTable: "ScoringModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoringScales",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoringModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringScales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringScales_ScoringModels_ScoringModelId",
                        column: x => x.ScoringModelId,
                        principalSchema: "App",
                        principalTable: "ScoringModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoringModelCriteria",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoringModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Token = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ScaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringModelCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringModelCriteria_ScoringModels_ScoringModelId",
                        column: x => x.ScoringModelId,
                        principalSchema: "App",
                        principalTable: "ScoringModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoringModelCriteria_ScoringScales_ScaleId",
                        column: x => x.ScaleId,
                        principalSchema: "App",
                        principalTable: "ScoringScales",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ScoringRatingLevels",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoringScaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringRatingLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringRatingLevels_ScoringScales_ScoringScaleId",
                        column: x => x.ScoringScaleId,
                        principalSchema: "App",
                        principalTable: "ScoringScales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoringModelCriteria_ScaleId",
                schema: "App",
                table: "ScoringModelCriteria",
                column: "ScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoringModelCriteria_ScoringModelId",
                schema: "App",
                table: "ScoringModelCriteria",
                column: "ScoringModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoringModelOutputs_ScoringModelId",
                schema: "App",
                table: "ScoringModelOutputs",
                column: "ScoringModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoringModels_State",
                schema: "App",
                table: "ScoringModels",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_ScoringRatingLevels_ScoringScaleId",
                schema: "App",
                table: "ScoringRatingLevels",
                column: "ScoringScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoringScales_ScoringModelId",
                schema: "App",
                table: "ScoringScales",
                column: "ScoringModelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoringModelCriteria",
                schema: "App");

            migrationBuilder.DropTable(
                name: "ScoringModelOutputs",
                schema: "App");

            migrationBuilder.DropTable(
                name: "ScoringRatingLevels",
                schema: "App");

            migrationBuilder.DropTable(
                name: "ScoringScales",
                schema: "App");

            migrationBuilder.DropTable(
                name: "ScoringModels",
                schema: "App");
        }
    }
}
