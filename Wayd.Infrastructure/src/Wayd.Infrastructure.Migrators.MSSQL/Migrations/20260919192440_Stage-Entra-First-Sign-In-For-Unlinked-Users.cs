using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class StageEntraFirstSignInForUnlinkedUsers : Migration
    {
        // Admin-created Entra users were never given a way to link on first sign-in once linking an existing
        // account by email was denied, so they are stuck. Stage the link new ones now get at creation: the
        // provider's tenant, completed by the first sign-in from it whose UPN/email matches.
        //
        // Only users that have never had any identity row, and only when the provider allows a single tenant.
        // With several, the tenant is the admin's choice and nothing here can make it; they set it from the
        // user's page (Set Sign-in Tenant).
        //
        // Idempotent: a user already staged is skipped.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE u
                SET u.[PendingMigrationTenantId] = t.[TenantId],
                    u.[PendingMigrationStagedAt] = SYSUTCDATETIME()
                FROM [Identity].[Users] u
                CROSS APPLY (
                    SELECT JSON_VALUE(p.[AllowedTenantIds], '$[0]') AS [TenantId]
                    FROM [Identity].[OidcProviders] p
                    WHERE p.[Name] = N'MicrosoftEntraId'
                        AND (SELECT COUNT(*) FROM OPENJSON(p.[AllowedTenantIds])) = 1
                ) t
                WHERE u.[LoginProvider] = N'MicrosoftEntraId'
                    AND u.[PendingMigrationTenantId] IS NULL
                    AND t.[TenantId] IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [Identity].[UserIdentities] ui
                        WHERE ui.[UserId] = u.[Id]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the previous build completes the same staged link on first sign-in, and a staged
            // row is indistinguishable from one an admin staged after this ran.
        }
    }
}
