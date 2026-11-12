using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHangfireDashboardPermission : Migration
    {
        /// <summary>
        /// Removes the Hangfire dashboard permission from every role. The dashboard is no longer served and the
        /// permission no longer exists, so stored roles would otherwise keep a claim the application does not define.
        /// </summary>
        public const string RemoveHangfirePermissionSql = """
            DELETE FROM [Identity].[RoleClaims]
            WHERE [ClaimType] = 'permission'
              AND [ClaimValue] = 'Permissions.Hangfire.View';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RemoveHangfirePermissionSql);
        }

        /// <inheritdoc />
        /// <remarks>Not undone: the removed permission no longer exists.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
