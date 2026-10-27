using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeDeskChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every desk offered every channel before a desk could choose, so existing desks keep doing so.
            migrationBuilder.AddColumn<bool>(
                name: "OffersAllChannels",
                schema: "DemandManagement",
                table: "IntakeDesks",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "IntakeDeskChannels",
                schema: "DemandManagement",
                columns: table => new
                {
                    DeskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeDeskChannels", x => new { x.DeskId, x.ChannelId });
                    table.ForeignKey(
                        name: "FK_IntakeDeskChannels_IntakeChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeDeskChannels_IntakeDesks_DeskId",
                        column: x => x.DeskId,
                        principalSchema: "DemandManagement",
                        principalTable: "IntakeDesks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeDeskChannels_ChannelId",
                schema: "DemandManagement",
                table: "IntakeDeskChannels",
                column: "ChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeDeskChannels",
                schema: "DemandManagement");

            migrationBuilder.DropColumn(
                name: "OffersAllChannels",
                schema: "DemandManagement",
                table: "IntakeDesks");
        }
    }
}
