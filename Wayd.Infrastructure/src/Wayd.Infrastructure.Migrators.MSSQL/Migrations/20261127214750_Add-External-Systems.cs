using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalSystems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExternalSystemId",
                schema: "AppIntegrations",
                table: "Connections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalSystems",
                schema: "AppIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Connector = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    SystemId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalSystems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Connections_ExternalSystemId",
                schema: "AppIntegrations",
                table: "Connections",
                column: "ExternalSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalSystems_Connector_SystemId",
                schema: "AppIntegrations",
                table: "ExternalSystems",
                columns: new[] { "Connector", "SystemId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Connections_ExternalSystems_ExternalSystemId",
                schema: "AppIntegrations",
                table: "Connections",
                column: "ExternalSystemId",
                principalSchema: "AppIntegrations",
                principalTable: "ExternalSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Connections_ExternalSystems_ExternalSystemId",
                schema: "AppIntegrations",
                table: "Connections");

            migrationBuilder.DropTable(
                name: "ExternalSystems",
                schema: "AppIntegrations");

            migrationBuilder.DropIndex(
                name: "IX_Connections_ExternalSystemId",
                schema: "AppIntegrations",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "ExternalSystemId",
                schema: "AppIntegrations",
                table: "Connections");
        }
    }
}
