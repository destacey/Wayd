import { OidcProviderDto, OidcProviderType } from '@/src/services/wayd-api'

export interface TenantMigrationAccess {
  /** Whether the "Migrate Users to New Tenant" action is offered. */
  canMigrateUsers: boolean
  /** Whether the Active Migrations section exists. */
  showActiveMigrations: boolean
}

export interface TenantMigrationAccessOptions {
  provider: OidcProviderDto | undefined
  /** `Permissions.Users.View` — reading who is mid-migration. */
  canViewUsers: boolean
  /** `Permissions.Users.Update` — staging a migration. */
  canStageMigration: boolean
}

/**
 * Whether tenant migration applies to this provider, and to this viewer.
 *
 * Two gates that are easy to conflate. The feature only exists for a
 * multi-tenant Entra provider — there must be at least two allowed tenants to
 * move users *between*, so a single-tenant one has nowhere to migrate to. And
 * the two halves take different permissions: staging a migration writes to
 * users, while the Active Migrations list only reads them.
 */
export const getTenantMigrationAccess = ({
  provider,
  canViewUsers,
  canStageMigration,
}: TenantMigrationAccessOptions): TenantMigrationAccess => {
  const isMultiTenantEntra =
    provider?.providerType === OidcProviderType.MicrosoftEntraId &&
    (provider?.allowedTenantIds?.length ?? 0) >= 2

  return {
    canMigrateUsers: isMultiTenantEntra && canStageMigration,
    showActiveMigrations: isMultiTenantEntra && canViewUsers,
  }
}

export default getTenantMigrationAccess
