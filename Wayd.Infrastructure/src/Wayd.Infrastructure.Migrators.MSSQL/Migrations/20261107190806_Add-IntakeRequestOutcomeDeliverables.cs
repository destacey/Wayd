using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeRequestOutcomeDeliverables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeRequestOutcomeDeliverables",
                schema: "DemandManagement",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliverableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRequestOutcomeDeliverables", x => new { x.RequestId, x.TargetId, x.DeliverableId });
                    table.ForeignKey(
                        name: "FK_IntakeRequestOutcomeDeliverables_IntakeRequestOutcomes_RequestId_TargetId",
                        columns: x => new { x.RequestId, x.TargetId },
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequestOutcomes",
                        principalColumns: new[] { "RequestId", "TargetId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRequestOutcomeDeliverables_DeliverableId",
                schema: "DemandManagement",
                table: "IntakeRequestOutcomeDeliverables",
                column: "DeliverableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeRequestOutcomeDeliverables",
                schema: "DemandManagement");
        }
    }
}
