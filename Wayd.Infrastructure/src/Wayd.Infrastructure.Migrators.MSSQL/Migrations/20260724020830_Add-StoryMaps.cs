using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoryMaps",
                schema: "Planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Deleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMaps", x => x.Id);
                    table.UniqueConstraint("AK_StoryMaps_Key", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "StoryMapGoals",
                schema: "Planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryMapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PersonaIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMapGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryMapGoals_StoryMaps_StoryMapId",
                        column: x => x.StoryMapId,
                        principalSchema: "Planning",
                        principalTable: "StoryMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryMapLanes",
                schema: "Planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryMapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: true),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMapLanes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryMapLanes_StoryMaps_StoryMapId",
                        column: x => x.StoryMapId,
                        principalSchema: "Planning",
                        principalTable: "StoryMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryMapPersonas",
                schema: "Planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryMapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Color = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMapPersonas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryMapPersonas_StoryMaps_StoryMapId",
                        column: x => x.StoryMapId,
                        principalSchema: "Planning",
                        principalTable: "StoryMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryMapSteps",
                schema: "Planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PersonaIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMapSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryMapSteps_StoryMapGoals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "Planning",
                        principalTable: "StoryMapGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryMapTasks",
                schema: "Planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LaneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LinkedWorkItemId = table.Column<int>(type: "int", nullable: true),
                    PersonaIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Checklist = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryMapTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryMapTasks_StoryMapLanes_LaneId",
                        column: x => x.LaneId,
                        principalSchema: "Planning",
                        principalTable: "StoryMapLanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryMapTasks_StoryMapSteps_StepId",
                        column: x => x.StepId,
                        principalSchema: "Planning",
                        principalTable: "StoryMapSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapGoals_StoryMapId",
                schema: "Planning",
                table: "StoryMapGoals",
                column: "StoryMapId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapLanes_StoryMapId",
                schema: "Planning",
                table: "StoryMapLanes",
                column: "StoryMapId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapPersonas_StoryMapId",
                schema: "Planning",
                table: "StoryMapPersonas",
                column: "StoryMapId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMaps_Id_IsDeleted",
                schema: "Planning",
                table: "StoryMaps",
                columns: new[] { "Id", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMaps_Key_IsDeleted",
                schema: "Planning",
                table: "StoryMaps",
                columns: new[] { "Key", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMaps_OwnerId_IsDeleted",
                schema: "Planning",
                table: "StoryMaps",
                columns: new[] { "OwnerId", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapSteps_GoalId",
                schema: "Planning",
                table: "StoryMapSteps",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapTasks_LaneId",
                schema: "Planning",
                table: "StoryMapTasks",
                column: "LaneId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryMapTasks_StepId",
                schema: "Planning",
                table: "StoryMapTasks",
                column: "StepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryMapPersonas",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "StoryMapTasks",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "StoryMapLanes",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "StoryMapSteps",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "StoryMapGoals",
                schema: "Planning");

            migrationBuilder.DropTable(
                name: "StoryMaps",
                schema: "Planning");
        }
    }
}
