using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMemberRoleCountsTowardDeliveryCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // True for the roles that already exist: every role counted before the setting did.
            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardDeliveryCapacity",
                schema: "Organization",
                table: "TeamMemberRoles",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountsTowardDeliveryCapacity",
                schema: "Organization",
                table: "TeamMemberRoles");
        }
    }
}
