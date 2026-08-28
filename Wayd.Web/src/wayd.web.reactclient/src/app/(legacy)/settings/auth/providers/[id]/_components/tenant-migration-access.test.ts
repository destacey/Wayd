import { OidcProviderDto, OidcProviderType } from '@/src/services/wayd-api'
import { getTenantMigrationAccess } from './tenant-migration-access'

const provider = (overrides: Partial<OidcProviderDto> = {}): OidcProviderDto =>
  ({
    id: 'a1b2c300-0000-0000-0000-000000000001',
    name: 'acme-entra',
    displayName: 'Acme Entra',
    providerType: OidcProviderType.MicrosoftEntraId,
    authority: 'https://login.example/acme',
    clientId: 'client-1',
    audience: 'api://wayd',
    scopes: ['openid'],
    allowedTenantIds: ['tenant-a', 'tenant-b'],
    clockSkewSeconds: 300,
    isEnabled: true,
    allowAutoRegistration: false,
    ...overrides,
  }) as OidcProviderDto

const allPermissions = { canViewUsers: true, canStageMigration: true }

describe('getTenantMigrationAccess', () => {
  describe('provider eligibility', () => {
    it('allows both halves for a multi-tenant Entra provider', () => {
      // Arrange / Act
      const access = getTenantMigrationAccess({
        provider: provider(),
        ...allPermissions,
      })

      // Assert
      expect(access).toEqual({
        canMigrateUsers: true,
        showActiveMigrations: true,
      })
    })

    it('denies a single-tenant Entra provider', () => {
      // Arrange / Act — with one allowed tenant there is nowhere to migrate to
      const access = getTenantMigrationAccess({
        provider: provider({ allowedTenantIds: ['tenant-a'] }),
        ...allPermissions,
      })

      // Assert
      expect(access).toEqual({
        canMigrateUsers: false,
        showActiveMigrations: false,
      })
    })

    it('denies a provider with no allowed tenants', () => {
      // Arrange / Act
      const access = getTenantMigrationAccess({
        provider: provider({ allowedTenantIds: undefined }),
        ...allPermissions,
      })

      // Assert
      expect(access.showActiveMigrations).toBe(false)
    })

    it('denies a generic OIDC provider however many tenants it lists', () => {
      // Arrange / Act — tenant migration is an Entra concept
      const access = getTenantMigrationAccess({
        provider: provider({
          providerType: OidcProviderType.GenericOidc,
          allowedTenantIds: ['tenant-a', 'tenant-b'],
        }),
        ...allPermissions,
      })

      // Assert
      expect(access).toEqual({
        canMigrateUsers: false,
        showActiveMigrations: false,
      })
    })

    it('denies everything while the provider is still loading', () => {
      // Arrange / Act
      const access = getTenantMigrationAccess({
        provider: undefined,
        ...allPermissions,
      })

      // Assert
      expect(access).toEqual({
        canMigrateUsers: false,
        showActiveMigrations: false,
      })
    })
  })

  describe('permissions', () => {
    it('shows the list without offering to stage one', () => {
      // Arrange / Act — reading who is mid-migration takes Users.View; staging
      // one writes to users and takes Users.Update.
      const access = getTenantMigrationAccess({
        provider: provider(),
        canViewUsers: true,
        canStageMigration: false,
      })

      // Assert
      expect(access).toEqual({
        canMigrateUsers: false,
        showActiveMigrations: true,
      })
    })

    it('offers to stage one without showing the list', () => {
      // Arrange / Act — the inverse, which is unusual but permitted
      const access = getTenantMigrationAccess({
        provider: provider(),
        canViewUsers: false,
        canStageMigration: true,
      })

      // Assert
      expect(access).toEqual({
        canMigrateUsers: true,
        showActiveMigrations: false,
      })
    })

    it('denies both to a viewer with neither permission', () => {
      // Arrange / Act
      const access = getTenantMigrationAccess({
        provider: provider(),
        canViewUsers: false,
        canStageMigration: false,
      })

      // Assert
      expect(access).toEqual({
        canMigrateUsers: false,
        showActiveMigrations: false,
      })
    })
  })
})
