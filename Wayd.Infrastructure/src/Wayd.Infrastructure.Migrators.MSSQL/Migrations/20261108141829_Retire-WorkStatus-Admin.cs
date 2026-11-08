using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class RetireWorkStatusAdmin : Migration
    {
        /// <summary>
        /// Copies each shared work status's description onto every status of the same name in a managed work item
        /// workflow, where that status has none yet.
        /// </summary>
        /// <remarks>
        /// A work status was shared by every workflow that held it, so its description belonged to each of the
        /// statuses the sync made from it; a workflow Wayd owns was never made from one. A status that already has
        /// a description keeps it, so a second run changes nothing. The copy is trimmed of spaces, tabs and line
        /// breaks, and cut to the 512 characters a workflow status holds, as the domain's setter would store it.
        /// <para>
        /// Only a database the sync has already copied into the engine has statuses to fill. Statuses the sync
        /// creates after this runs start with no description; #1238, which moves the sync onto the engine, copies
        /// those before Work's statuses are dropped.
        /// </para>
        /// </remarks>
        public const string CopyDescriptionsSql = """
            UPDATE s
            SET s.[Description] = TRIM(CHAR(9) + CHAR(10) + CHAR(13) + CHAR(32) FROM LEFT(TRIM(CHAR(9) + CHAR(10) + CHAR(13) + CHAR(32) FROM ws.[Description]), 512))
            FROM [StatusWorkflows].[WorkflowStatuses] s
            INNER JOIN [StatusWorkflows].[StatusWorkflows] w ON w.[Id] = s.[WorkflowId]
            INNER JOIN [Work].[WorkStatuses] ws ON ws.[Name] = s.[Name]
            WHERE w.[OwnerType] = 'work.item'
              AND w.[Ownership] = 'Managed'
              AND s.[Description] IS NULL
              AND ws.[IsDeleted] = 0
              AND ws.[Description] IS NOT NULL
              AND TRIM(CHAR(9) + CHAR(10) + CHAR(13) + CHAR(32) FROM ws.[Description]) <> '';
            """;

        /// <summary>
        /// Removes the work status permissions from every role. The permissions no longer exist, so stored roles
        /// would otherwise keep claims the application does not define.
        /// </summary>
        public const string RemoveWorkStatusPermissionsSql = """
            DELETE FROM [Identity].[RoleClaims]
            WHERE [ClaimType] = 'permission'
              AND [ClaimValue] IN (
                  'Permissions.WorkStatuses.View',
                  'Permissions.WorkStatuses.Create',
                  'Permissions.WorkStatuses.Update',
                  'Permissions.WorkStatuses.Delete');
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CopyDescriptionsSql);
            migrationBuilder.Sql(RemoveWorkStatusPermissionsSql);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Neither change is undone: a copied description cannot be told from one written afterwards, and the
        /// removed permissions no longer exist.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
