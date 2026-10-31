using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class UniqueWorkProcessExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkProcesses_ExternalId",
                schema: "Work",
                table: "WorkProcesses",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkProcesses_ExternalId",
                schema: "Work",
                table: "WorkProcesses");
        }
    }
}
