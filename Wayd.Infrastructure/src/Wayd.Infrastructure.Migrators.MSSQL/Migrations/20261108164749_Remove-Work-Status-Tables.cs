using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWorkStatusTables : Migration
    {
        /// <summary>
        /// Keeps the description of each shared work status that has one, by name, before the shared statuses are
        /// dropped, so the sync can give it to each engine status of that name.
        /// </summary>
        /// <remarks>
        /// On an upgrade from before work items moved onto the status workflow engine, the engine holds no work
        /// item statuses when this runs, so the descriptions cannot be copied onto them here; the sync copies each
        /// as it creates the status, through the workflow so the change is recorded. Trimmed of spaces, tabs and
        /// line breaks, and cut to the 512 characters a workflow status holds, as the domain's setter would store
        /// it. A deleted status's description is not kept.
        /// </remarks>
        public const string KeepDescriptionsSql = """
            INSERT INTO [Work].[LegacyWorkStatusDescriptions] ([Name], [Description])
            SELECT ws.[Name], TRIM(CHAR(9) + CHAR(10) + CHAR(13) + CHAR(32) FROM LEFT(TRIM(CHAR(9) + CHAR(10) + CHAR(13) + CHAR(32) FROM ws.[Description]), 512))
            FROM [Work].[WorkStatuses] ws
            WHERE ws.[IsDeleted] = 0
              AND ws.[Description] IS NOT NULL
              AND TRIM(CHAR(9) + CHAR(10) + CHAR(13) + CHAR(32) FROM ws.[Description]) <> '';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormerNames",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "LegacyWorkStatusDescriptions",
                schema: "Work",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyWorkStatusDescriptions", x => x.Name);
                });

            migrationBuilder.Sql(KeepDescriptionsSql);

            migrationBuilder.DropTable(
                name: "WorkflowSchemes",
                schema: "Work");

            migrationBuilder.DropTable(
                name: "WorkProcessSchemes",
                schema: "Work");

            migrationBuilder.DropTable(
                name: "WorkStatuses",
                schema: "Work");

            migrationBuilder.DropTable(
                name: "Workflows",
                schema: "Work");
        }

        /// <inheritdoc />
        /// <remarks>
        /// The shared statuses and Work's workflows come back empty: nothing writes them any more, and the sync
        /// keeps work item statuses in the status workflow engine.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyWorkStatusDescriptions",
                schema: "Work");

            migrationBuilder.DropColumn(
                name: "FormerNames",
                schema: "StatusWorkflows",
                table: "WorkflowStatuses");

            migrationBuilder.CreateTable(
                name: "Workflows",
                schema: "Work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Deleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Ownership = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                    table.UniqueConstraint("AK_Workflows_Key", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "WorkStatuses",
                schema: "Work",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Deleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkProcessSchemes",
                schema: "Work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkTypeId = table.Column<int>(type: "int", nullable: false),
                    Deleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkProcessSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkProcessSchemes_WorkProcesses_WorkProcessId",
                        column: x => x.WorkProcessId,
                        principalSchema: "Work",
                        principalTable: "WorkProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkProcessSchemes_WorkTypes_WorkTypeId",
                        column: x => x.WorkTypeId,
                        principalSchema: "Work",
                        principalTable: "WorkTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkProcessSchemes_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "Work",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSchemes",
                schema: "Work",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkStatusId = table.Column<int>(type: "int", nullable: false),
                    Deleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    WorkStatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSchemes_WorkStatuses_WorkStatusId",
                        column: x => x.WorkStatusId,
                        principalSchema: "Work",
                        principalTable: "WorkStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowSchemes_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "Work",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Id_IsDeleted",
                schema: "Work",
                table: "Workflows",
                columns: new[] { "Id", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_IsActive_IsDeleted",
                schema: "Work",
                table: "Workflows",
                columns: new[] { "IsActive", "IsDeleted" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Key_IsDeleted",
                schema: "Work",
                table: "Workflows",
                columns: new[] { "Key", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemes_Id_IsDeleted",
                schema: "Work",
                table: "WorkflowSchemes",
                columns: new[] { "Id", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemes_WorkflowId_IsDeleted",
                schema: "Work",
                table: "WorkflowSchemes",
                columns: new[] { "WorkflowId", "IsDeleted" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Id", "WorkStatusId", "WorkStatusCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemes_WorkStatusId",
                schema: "Work",
                table: "WorkflowSchemes",
                column: "WorkStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcessSchemes_Id_IsDeleted",
                schema: "Work",
                table: "WorkProcessSchemes",
                columns: new[] { "Id", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcessSchemes_WorkflowId",
                schema: "Work",
                table: "WorkProcessSchemes",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcessSchemes_WorkProcessId_IsDeleted",
                schema: "Work",
                table: "WorkProcessSchemes",
                columns: new[] { "WorkProcessId", "IsDeleted" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Id", "WorkTypeId", "WorkflowId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkProcessSchemes_WorkTypeId",
                schema: "Work",
                table: "WorkProcessSchemes",
                column: "WorkTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkStatuses_Id_IsDeleted",
                schema: "Work",
                table: "WorkStatuses",
                columns: new[] { "Id", "IsDeleted" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkStatuses_IsActive_IsDeleted",
                schema: "Work",
                table: "WorkStatuses",
                columns: new[] { "IsActive", "IsDeleted" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Id", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkStatuses_Name",
                schema: "Work",
                table: "WorkStatuses",
                column: "Name",
                unique: true);
        }
    }
}
