using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeDeskMemberIsTriager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTriager",
                schema: "DemandManagement",
                table: "IntakeDeskMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTriager",
                schema: "DemandManagement",
                table: "IntakeDeskMembers");
        }
    }
}
