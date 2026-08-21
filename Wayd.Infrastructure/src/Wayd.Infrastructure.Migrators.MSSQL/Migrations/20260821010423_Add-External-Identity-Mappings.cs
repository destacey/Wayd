using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddExternalIdentityMappings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExternalIdentityMappings",
            schema: "AppIntegrations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Connector = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Handle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalIdentityMappings", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExternalIdentityMappings_Employees_EmployeeId",
                    column: x => x.EmployeeId,
                    principalSchema: "Organization",
                    principalTable: "Employees",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExternalIdentityMappings_ConnectionId_ExternalId",
            schema: "AppIntegrations",
            table: "ExternalIdentityMappings",
            columns: new[] { "ConnectionId", "ExternalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExternalIdentityMappings_ConnectionId_Status",
            schema: "AppIntegrations",
            table: "ExternalIdentityMappings",
            columns: new[] { "ConnectionId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_ExternalIdentityMappings_EmployeeId",
            schema: "AppIntegrations",
            table: "ExternalIdentityMappings",
            column: "EmployeeId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ExternalIdentityMappings",
            schema: "AppIntegrations");
    }
}
