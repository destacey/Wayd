using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTeamLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductTeams",
                schema: "ProductManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    Type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Watermarks = table.Column<string>(type: "varchar(1024)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTeams", x => x.Id);
                    table.UniqueConstraint("AK_ProductTeams_Key", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ProductTeamLinks",
                schema: "ProductManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTeamLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductTeamLinks_ProductTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "ProductManagement",
                        principalTable: "ProductTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductTeamLinks_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ProductManagement",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductTeamLinks_ProductId_TeamId",
                schema: "ProductManagement",
                table: "ProductTeamLinks",
                columns: new[] { "ProductId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductTeamLinks_TeamId",
                schema: "ProductManagement",
                table: "ProductTeamLinks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTeams_Code",
                schema: "ProductManagement",
                table: "ProductTeams",
                column: "Code",
                unique: true)
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Name", "Type", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductTeamLinks",
                schema: "ProductManagement");

            migrationBuilder.DropTable(
                name: "ProductTeams",
                schema: "ProductManagement");
        }
    }
}
