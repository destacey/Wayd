import { renderHook } from '@testing-library/react'
import type { ItemType, MenuItemType } from 'antd/es/menu/interface'

const mockAuth = { employeeId: 'emp-1' as string | null }

/** Feature flags the menu reads, keyed by flag name. Default off, as the suite assumed before. */
const mockFlags: Record<string, boolean> = {}

/**
 * Claims the signed-in user holds. `null` means "every claim", which is what most cases want; a set
 * narrows it so a test can prove which permission actually guards an entry.
 */
const mockClaims: { held: Set<string> | null } = { held: null }

jest.mock('../../../components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasClaim: (_type: string, value: string) =>
      mockClaims.held === null || mockClaims.held.has(value),
  }),
}))

jest.mock('../../../hooks', () => ({
  useFeatureFlag: (flag: string) => ({
    isEnabled: mockFlags[flag] ?? false,
    isLoading: false,
  }),
  useLinkedEmployee: () => ({
    employeeId: mockAuth.employeeId,
    hasLinkedEmployee: mockAuth.employeeId !== null,
  }),
}))

import useAppMenuItems from './use-app-menu-items'

/**
 * Recursively collects every menu key, including nested section children. Keys rather than labels:
 * leaf labels are rendered as `<Link>` elements, so only section headers carry a plain string.
 */
function keysOf(items: ItemType<MenuItemType>[]): string[] {
  return items.flatMap((item) => {
    if (item == null) return []
    const self = item.key != null ? [String(item.key)] : []
    const children =
      'children' in item && Array.isArray(item.children)
        ? keysOf(item.children as ItemType<MenuItemType>[])
        : []
    return [...self, ...children]
  })
}

describe('useAppMenuItems', () => {
  beforeEach(() => {
    mockAuth.employeeId = 'emp-1'
    for (const flag of Object.keys(mockFlags)) delete mockFlags[flag]
    mockClaims.held = null
  })

  it('includes My Projects when the account is linked to an employee', () => {
    const { result } = renderHook(() => useAppMenuItems())

    expect(keysOf(result.current.menuItems)).toContain('ppm.dashboards.my-projects')
  })

  it('omits My Projects when the account has no linked employee', () => {
    // Project roles are held by the employee record, so the page would always be empty.
    mockAuth.employeeId = null

    const { result } = renderHook(() => useAppMenuItems())

    expect(keysOf(result.current.menuItems)).not.toContain('ppm.dashboards.my-projects')
  })

  it('keeps the rest of the PPM section when My Projects is omitted', () => {
    // Dropping the item must not take its sibling entries with it.
    mockAuth.employeeId = null

    const { result } = renderHook(() => useAppMenuItems())
    const keys = keysOf(result.current.menuItems)

    expect(keys).toContain('ppm.portfolios')
    expect(keys).toContain('ppm.projects')
  })

  it('omits Product Management while its feature flag is off', () => {
    const { result } = renderHook(() => useAppMenuItems())

    expect(keysOf(result.current.menuItems)).not.toContain('product.products')
  })

  it('includes Product Management when its feature flag is on', () => {
    // The whole section is gated, not just its pages: the API 404s until the module is enabled, so
    // offering the menu entry would lead somewhere that does not answer.
    mockFlags['product-management'] = true

    const { result } = renderHook(() => useAppMenuItems())

    const keys = keysOf(result.current.menuItems)
    expect(keys).toContain('product')
    expect(keys).toContain('product.products')
  })

  it('omits Versions while the Product Management flag is off', () => {
    // Versions ride the same module flag as the catalog: delivery is schema-separated to keep a
    // later module split cheap, but one module answers for both and its endpoints 404 together.
    const { result } = renderHook(() => useAppMenuItems())

    expect(keysOf(result.current.menuItems)).not.toContain('product.versions')
  })

  it('includes Versions alongside Products when the flag is on', () => {
    mockFlags['product-management'] = true

    const { result } = renderHook(() => useAppMenuItems())

    const keys = keysOf(result.current.menuItems)
    expect(keys).toContain('product')
    expect(keys).toContain('product.products')
    expect(keys).toContain('product.versions')
  })

  it('guards Releases on its own permission, not the Delivery one', () => {
    // Releases carry Permissions.Releases.* rather than riding Delivery's, because the audience
    // differs: a product manager drafting 2026.07 is a different person from whoever records that
    // the pipeline ran. Someone holding only the Delivery claim sees the engineering records and
    // not the announcement.
    mockFlags['product-management'] = true
    mockClaims.held = new Set(['Permissions.Delivery.View'])

    const { result } = renderHook(() => useAppMenuItems())

    const keys = keysOf(result.current.menuItems)
    expect(keys).toContain('product.versions')
    expect(keys).not.toContain('product.releases')
  })

  it('includes Releases for someone holding only the Releases permission', () => {
    mockFlags['product-management'] = true
    mockClaims.held = new Set(['Permissions.Releases.View'])

    const { result } = renderHook(() => useAppMenuItems())

    const keys = keysOf(result.current.menuItems)
    expect(keys).toContain('product.releases')
    // The section survives on the Releases claim alone, with the engineering records left out.
    expect(keys).toContain('product')
    expect(keys).not.toContain('product.versions')
  })

  it('guards Versions on its own permission, not the catalog one', () => {
    // One section, but not one permission: someone who can see products need not be able to see
    // delivery records, and offering the entry anyway leads to a page that refuses them.
    mockFlags['product-management'] = true
    mockClaims.held = new Set(['Permissions.Products.View'])

    const { result } = renderHook(() => useAppMenuItems())

    const keys = keysOf(result.current.menuItems)
    expect(keys).toContain('product.products')
    expect(keys).not.toContain('product.versions')
    // The section survives, because Products still passes.
    expect(keys).toContain('product')
  })

  it('keys every child under the section it is rendered in', () => {
    // `findMenuKeysByPathname` opens the section named by the part of a key before the first dot, so
    // a child keyed outside its parent asks to open a section that is not in the tree — and antd
    // renders the item selected with its parent collapsed.
    //
    // Two entries shipped that way: the delivery items were keyed `delivery.*` inside the `product`
    // section, and Strategic Themes was keyed `strategy.*` inside `ppm`. Both looked reasonable in
    // isolation, which is why this is asserted over the whole tree rather than per entry.
    mockFlags['product-management'] = true
    mockFlags['planning-poker'] = true
    mockFlags['story-maps'] = true

    const { result } = renderHook(() => useAppMenuItems())

    const mismatches: string[] = []
    const walk = (items: ItemType<MenuItemType>[], parentKey: string | null) => {
      for (const item of items) {
        if (item == null || item.key == null) continue
        const key = String(item.key)
        // Dividers carry synthetic keys that name no route and open nothing.
        const isDivider = 'type' in item && item.type === 'divider'

        if (parentKey && !isDivider && key.split('.')[0] !== parentKey) {
          mismatches.push(`${key} is inside ${parentKey}`)
        }

        if ('children' in item && Array.isArray(item.children)) {
          walk(item.children as ItemType<MenuItemType>[], key)
        }
      }
    }
    walk(result.current.menuItems, null)

    expect(mismatches).toEqual([])
  })
})
