using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeRequestTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeRequestTagCategories",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AllowsMany = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestTagCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntakeRequestTags",
                schema: "DemandManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeRequestTags_IntakeRequestTagCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequestTagCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeRequestTagAssignments",
                schema: "DemandManagement",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestTagAssignments", x => new { x.RequestId, x.TagId });
                    table.ForeignKey(
                        name: "FK_IntakeRequestTagAssignments_IntakeRequestTagCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequestTagCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequestTagAssignments_IntakeRequestTags_TagId",
                        column: x => x.TagId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequestTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeRequestTagAssignments_IntakeRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestTagAssignments_CategoryId",
                schema: "DemandManagement",
                table: "IntakeRequestTagAssignments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestTagAssignments_TagId",
                schema: "DemandManagement",
                table: "IntakeRequestTagAssignments",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestTagCategories_Name",
                schema: "DemandManagement",
                table: "IntakeRequestTagCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestTags_CategoryId_Name",
                schema: "DemandManagement",
                table: "IntakeRequestTags",
                columns: new[] { "CategoryId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeRequestTagAssignments",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeRequestTags",
                schema: "DemandManagement");

            migrationBuilder.DropTable(
                name: "IntakeRequestTagCategories",
                schema: "DemandManagement");
        }
    }
}
