using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTransitionConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransitionConditions",
                schema: "StatusWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransitionConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransitionConditions_WorkflowTransitions_TransitionId",
                        column: x => x.TransitionId,
                        principalSchema: "StatusWorkflows",
                        principalTable: "WorkflowTransitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransitionConditions_TransitionId_Kind",
                schema: "StatusWorkflows",
                table: "TransitionConditions",
                columns: new[] { "TransitionId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransitionConditions",
                schema: "StatusWorkflows");
        }
    }
}
