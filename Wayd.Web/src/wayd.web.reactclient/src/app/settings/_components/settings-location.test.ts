import type { ItemType, MenuItemType } from 'antd/es/menu/interface'
import { settingsLocation } from './use-settings-menu-items'

/** A leaf item, whose label is a `<Link>` wrapping its text. */
const leaf = (key: string, text: string) =>
  ({
    key,
    label: { props: { children: text } },
  }) as unknown as ItemType<MenuItemType>

/** A group, whose label is plain text. */
const group = (
  key: string,
  label: string,
  children: ItemType<MenuItemType>[],
) =>
  ({ key, label, children, type: 'group' }) as unknown as ItemType<MenuItemType>

const items = [
  group('access', 'Access', [
    leaf('access.users', 'Users'),
    leaf('access.roles', 'Roles'),
  ]),
  leaf('about', 'About'),
]

describe('settingsLocation', () => {
  it('names the group and page of a grouped item', () => {
    // Arrange
    // Act
    const location = settingsLocation(items, 'access.roles')

    // Assert
    expect(location).toEqual({ group: 'Access', page: 'Roles' })
  })

  it('names only the page of a top-level item', () => {
    // Arrange
    // Act
    const location = settingsLocation(items, 'about')

    // Assert
    expect(location).toEqual({ page: 'About' })
  })

  it('returns nothing for a key no item has', () => {
    // Arrange
    // Act
    const location = settingsLocation(items, 'missing')

    // Assert
    expect(location).toBeUndefined()
  })

  it('returns nothing when there is no key', () => {
    // Arrange
    // Act
    const location = settingsLocation(items, undefined)

    // Assert
    expect(location).toBeUndefined()
  })
})
