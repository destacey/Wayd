import { ItemType, MenuItemType } from 'antd/es/menu/interface'
import { firstSettingsRoute } from './use-settings-menu-items'

/** A leaf item, as `filterAndTransformMenuItem` produces one. */
const leaf = (key: string, route: string) =>
  ({ key, route, label: route }) as unknown as ItemType<MenuItemType>

/** A group, whose children are the destinations. */
const group = (key: string, children: ItemType<MenuItemType>[]) =>
  ({ key, children, type: 'group' }) as unknown as ItemType<MenuItemType>

describe('firstSettingsRoute', () => {
  it('returns the first destination in the first group', () => {
    // Arrange
    const items = [
      group('access', [
        leaf('access.users', '/settings/user-management/users'),
        leaf('access.roles', '/settings/user-management/roles'),
      ]),
      group('ppm', [leaf('ppm.scoring', '/settings/scoring/scoring-models')]),
    ]

    // Act / Assert
    expect(firstSettingsRoute(items)).toBe('/settings/user-management/users')
  })

  it('falls through a group the viewer cannot see anything in', () => {
    // Arrange — permission filtering drops a group's children, and the group
    // with them; this covers the case where an empty one survives.
    const items = [
      group('access', []),
      group('work-management', [
        leaf('wm.work-types', '/settings/work-management/work-types'),
      ]),
    ]

    // Act / Assert
    expect(firstSettingsRoute(items)).toBe('/settings/work-management/work-types')
  })

  it('follows whatever the viewer can actually see', () => {
    // Arrange — someone who can see work configuration but not users lands on
    // Work Types rather than being sent somewhere they would be bounced from.
    const items = [
      group('work-management', [
        leaf('wm.work-types', '/settings/work-management/work-types'),
      ]),
    ]

    // Act / Assert
    expect(firstSettingsRoute(items)).toBe('/settings/work-management/work-types')
  })

  it('returns nothing when the rail is empty', () => {
    // Arrange / Act / Assert — the page shows an explanation rather than
    // redirecting nowhere.
    expect(firstSettingsRoute([])).toBeUndefined()
  })

  it('returns nothing when every group is empty', () => {
    // Arrange / Act / Assert
    expect(firstSettingsRoute([group('a', []), group('b', [])])).toBeUndefined()
  })

  it('skips an item that carries no route', () => {
    // Arrange — a heading or divider is not somewhere to send anyone
    const items = [
      { key: 'divider', type: 'divider' } as ItemType<MenuItemType>,
      group('access', [leaf('access.users', '/settings/user-management/users')]),
    ]

    // Act / Assert
    expect(firstSettingsRoute(items)).toBe('/settings/user-management/users')
  })

  it('tolerates a null item', () => {
    // Arrange / Act / Assert — antd's ItemType admits null
    expect(
      firstSettingsRoute([null as unknown as ItemType<MenuItemType>]),
    ).toBeUndefined()
  })
})
