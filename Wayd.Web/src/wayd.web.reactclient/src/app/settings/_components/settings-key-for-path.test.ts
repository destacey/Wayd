import { settingsKeyForPath } from './use-settings-menu-items'

const routeKeyMap = new Map([
  ['/settings/user-management/users', 'access.users'],
  ['/settings/user-management/roles', 'access.roles'],
  ['/settings/status-workflows', 'system.status-workflows'],
])

describe('settingsKeyForPath', () => {
  it('matches a route exactly', () => {
    // Arrange
    const pathname = '/settings/user-management/roles'

    // Act
    const key = settingsKeyForPath(pathname, routeKeyMap)

    // Assert
    expect(key).toBe('access.roles')
  })

  it('matches a page beneath a route', () => {
    // Arrange
    const pathname = '/settings/user-management/users/42'

    // Act
    const key = settingsKeyForPath(pathname, routeKeyMap)

    // Assert
    expect(key).toBe('access.users')
  })

  it('prefers the longest route covering the path', () => {
    // Arrange
    const map = new Map([
      ['/settings/system', 'system'],
      ['/settings/system/general', 'system.general'],
    ])

    // Act
    const key = settingsKeyForPath('/settings/system/general', map)

    // Assert
    expect(key).toBe('system.general')
  })

  it('matches nothing for a settings page the menu omits', () => {
    // Arrange — a flag-off page, or one whose menu entry is still loading,
    // shares only `settings` with every route.
    const pathname = '/settings/imports'

    // Act
    const key = settingsKeyForPath(pathname, routeKeyMap)

    // Assert
    expect(key).toBeUndefined()
  })

  it('does not treat a shared name prefix as a parent route', () => {
    // Arrange
    const pathname = '/settings/status-workflows-archive'

    // Act
    const key = settingsKeyForPath(pathname, routeKeyMap)

    // Assert
    expect(key).toBeUndefined()
  })
})
