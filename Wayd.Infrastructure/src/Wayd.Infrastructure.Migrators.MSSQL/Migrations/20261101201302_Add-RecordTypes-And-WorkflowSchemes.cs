using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordTypesAndWorkflowSchemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecordTypes",
                schema: "StatusWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Ownership = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ExternalKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordTypes", x => x.Id);
                    table.UniqueConstraint("AK_RecordTypes_Key", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSchemes",
                schema: "StatusWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Ownership = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ExternalKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsImplicit = table.Column<bool>(type: "bit", nullable: false),
                    DefaultWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSchemes", x => x.Id);
                    table.UniqueConstraint("AK_WorkflowSchemes_Key", x => x.Key);
                    table.ForeignKey(
                        name: "FK_WorkflowSchemes_StatusWorkflows_DefaultWorkflowId",
                        column: x => x.DefaultWorkflowId,
                        principalSchema: "StatusWorkflows",
                        principalTable: "StatusWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSchemeEntries",
                schema: "StatusWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSchemeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSchemeEntries_RecordTypes_RecordTypeId",
                        column: x => x.RecordTypeId,
                        principalSchema: "StatusWorkflows",
                        principalTable: "RecordTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowSchemeEntries_StatusWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "StatusWorkflows",
                        principalTable: "StatusWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowSchemeEntries_WorkflowSchemes_SchemeId",
                        column: x => x.SchemeId,
                        principalSchema: "StatusWorkflows",
                        principalTable: "WorkflowSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordTypes_OwnerType_ExternalKey",
                schema: "StatusWorkflows",
                table: "RecordTypes",
                columns: new[] { "OwnerType", "ExternalKey" },
                unique: true,
                filter: "[ExternalKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecordTypes_OwnerType_Name",
                schema: "StatusWorkflows",
                table: "RecordTypes",
                columns: new[] { "OwnerType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemeEntries_RecordTypeId",
                schema: "StatusWorkflows",
                table: "WorkflowSchemeEntries",
                column: "RecordTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemeEntries_SchemeId_RecordTypeId",
                schema: "StatusWorkflows",
                table: "WorkflowSchemeEntries",
                columns: new[] { "SchemeId", "RecordTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemeEntries_WorkflowId",
                schema: "StatusWorkflows",
                table: "WorkflowSchemeEntries",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemes_DefaultWorkflowId",
                schema: "StatusWorkflows",
                table: "WorkflowSchemes",
                column: "DefaultWorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemes_DefaultWorkflowId_Implicit",
                schema: "StatusWorkflows",
                table: "WorkflowSchemes",
                column: "DefaultWorkflowId",
                unique: true,
                filter: "[IsImplicit] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSchemes_OwnerType_ExternalKey",
                schema: "StatusWorkflows",
                table: "WorkflowSchemes",
                columns: new[] { "OwnerType", "ExternalKey" },
                unique: true,
                filter: "[ExternalKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowSchemeEntries",
                schema: "StatusWorkflows");

            migrationBuilder.DropTable(
                name: "RecordTypes",
                schema: "StatusWorkflows");

            migrationBuilder.DropTable(
                name: "WorkflowSchemes",
                schema: "StatusWorkflows");
        }
    }
}
