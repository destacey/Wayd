using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddProductDependencies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProductDependencies",
            schema: "ProductManagement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DependsOnProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Strength = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                Start = table.Column<DateTime>(type: "date", nullable: false),
                End = table.Column<DateTime>(type: "date", nullable: true),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductDependencies", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductDependencies_Products_DependsOnProductId",
                    column: x => x.DependsOnProductId,
                    principalSchema: "ProductManagement",
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProductDependencies_Products_ProductId",
                    column: x => x.ProductId,
                    principalSchema: "ProductManagement",
                    principalTable: "Products",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProductDependencies_DependsOnProductId",
            schema: "ProductManagement",
            table: "ProductDependencies",
            column: "DependsOnProductId");

        migrationBuilder.CreateIndex(
            name: "IX_ProductDependencies_ProductId_DependsOnProductId",
            schema: "ProductManagement",
            table: "ProductDependencies",
            columns: new[] { "ProductId", "DependsOnProductId" },
            unique: true,
            filter: "[End] IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProductDependencies",
            schema: "ProductManagement");
    }
}
