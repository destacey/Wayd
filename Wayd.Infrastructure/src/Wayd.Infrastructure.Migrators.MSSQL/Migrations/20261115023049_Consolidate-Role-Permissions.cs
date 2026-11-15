using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateRolePermissions : Migration
    {
        /// <summary>
        /// Gives every role that could edit role permissions the one permission that now covers managing roles, and
        /// every role that could see them the permission that now shows them.
        /// </summary>
        /// <remarks>
        /// Only <c>RoleClaims.Update</c> maps to <c>Roles.Manage</c>: <c>Roles.Create</c>, <c>Roles.Update</c> and
        /// <c>Roles.Delete</c> alone could add, rename or delete an empty role, while <c>Roles.Manage</c> also chooses
        /// what roles grant, so mapping them would widen access on upgrade.
        /// </remarks>
        public const string GrantReplacementsSql = """
            INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [Created], [CreatedBy])
            SELECT DISTINCT old.[RoleId], 'permission', map.[NewValue], SYSUTCDATETIME(), 'ConsolidateRolePermissions'
            FROM [Identity].[RoleClaims] old
            INNER JOIN (VALUES
                ('Permissions.RoleClaims.Update', 'Permissions.Roles.Manage'),
                ('Permissions.RoleClaims.View', 'Permissions.Roles.View')
            ) map ([OldValue], [NewValue]) ON map.[OldValue] = old.[ClaimValue]
            WHERE old.[ClaimType] = 'permission'
              AND NOT EXISTS (
                  SELECT 1 FROM [Identity].[RoleClaims] held
                  WHERE held.[RoleId] = old.[RoleId]
                    AND held.[ClaimType] = 'permission'
                    AND held.[ClaimValue] = map.[NewValue]);
            """;

        /// <summary>
        /// Removes the permissions the catalog no longer defines: the four role permissions <c>Roles.Manage</c>
        /// replaced, <c>RoleClaims.View</c>, and the three user permissions no endpoint ever checked.
        /// </summary>
        public const string RemoveRetiredSql = """
            DELETE FROM [Identity].[RoleClaims]
            WHERE [ClaimType] = 'permission'
              AND [ClaimValue] IN (
                  'Permissions.Roles.Create',
                  'Permissions.Roles.Update',
                  'Permissions.Roles.Delete',
                  'Permissions.RoleClaims.View',
                  'Permissions.RoleClaims.Update',
                  'Permissions.Users.Search',
                  'Permissions.Users.Delete',
                  'Permissions.Users.Export');
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GrantReplacementsSql);
            migrationBuilder.Sql(RemoveRetiredSql);
        }

        /// <inheritdoc />
        /// <remarks>Not undone: the retired permissions no longer exist, and which role held which is not kept.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
