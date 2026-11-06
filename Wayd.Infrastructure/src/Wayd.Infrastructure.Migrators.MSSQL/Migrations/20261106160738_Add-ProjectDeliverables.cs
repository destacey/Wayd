using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDeliverables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revision",
                schema: "Ppm",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProjectDeliverables",
                schema: "Ppm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "date", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AcceptedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    StatusAliasValue = table.Column<int>(type: "int", nullable: false),
                    StatusTransitionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDeliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDeliverables_Employees_AcceptedById",
                        column: x => x.AcceptedById,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectDeliverables_Employees_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "Organization",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectDeliverables_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "Ppm",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_AcceptedById",
                schema: "Ppm",
                table: "ProjectDeliverables",
                column: "AcceptedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_OwnerId",
                schema: "Ppm",
                table: "ProjectDeliverables",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_ProjectId_Order",
                schema: "Ppm",
                table: "ProjectDeliverables",
                columns: new[] { "ProjectId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDeliverables_StatusWorkflowId",
                schema: "Ppm",
                table: "ProjectDeliverables",
                column: "StatusWorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDeliverables",
                schema: "Ppm");

            migrationBuilder.DropColumn(
                name: "Revision",
                schema: "Ppm",
                table: "Projects");
        }
    }
}
