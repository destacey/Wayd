using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeDeskTagCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every desk offered every tag axis before a desk could choose, so existing desks keep doing so.
            migrationBuilder.AddColumn<bool>(
                name: "OffersAllTagCategories",
                schema: "DemandManagement",
                table: "IntakeDesks",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "IntakeDeskTagCategories",
                schema: "DemandManagement",
                columns: table => new
                {
                    DeskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDeskTagCategories", x => new { x.DeskId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_IntakeDeskTagCategories_IntakeDesks_DeskId",
                        column: x => x.DeskId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeDesks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntakeDeskTagCategories_IntakeRequestTagCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeRequestTagCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDeskTagCategories_CategoryId",
                schema: "DemandManagement",
                table: "IntakeDeskTagCategories",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeDeskTagCategories",
                schema: "DemandManagement");

            migrationBuilder.DropColumn(
                name: "OffersAllTagCategories",
                schema: "DemandManagement",
                table: "IntakeDesks");
        }
    }
}
