using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddProjectStatusHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectStatusHistory",
            schema: "Ppm",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                ToStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ChangedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                Source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectStatusHistory", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectStatusHistory_Employees_ChangedByEmployeeId",
                    column: x => x.ChangedByEmployeeId,
                    principalSchema: "Organization",
                    principalTable: "Employees",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectStatusHistory_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalSchema: "Ppm",
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectStatusHistory_ChangedByEmployeeId",
            schema: "Ppm",
            table: "ProjectStatusHistory",
            column: "ChangedByEmployeeId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectStatusHistory_ProjectId_ChangedOn",
            schema: "Ppm",
            table: "ProjectStatusHistory",
            columns: new[] { "ProjectId", "ChangedOn" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectStatusHistory",
            schema: "Ppm");
    }
}
