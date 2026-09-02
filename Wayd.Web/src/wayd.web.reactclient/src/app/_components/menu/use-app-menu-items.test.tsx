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

    expect(keysOf(result.current.menuItems)).not.toContain('delivery.versions')
  })

  it('includes Versions alongside Products when the flag is on', () => {
    mockFlags['product-management'] = true

    const { result } = renderHook(() => useAppMenuItems())

    const keys = keysOf(result.current.menuItems)
    expect(keys).toContain('product')
    expect(keys).toContain('product.products')
    expect(keys).toContain('delivery.versions')
  })

  it('offers no Releases entry while the announcement screens are unbuilt', () => {
    // The Release record and Permissions.Releases.* both exist, but /delivery/releases has no page
    // until the announcement UI lands. A nav item that 404s is worse than a missing one.
    mockFlags['product-management'] = true
    mockClaims.held = new Set(['Permissions.Releases.View'])

    const { result } = renderHook(() => useAppMenuItems())

    expect(keysOf(result.current.menuItems)).not.toContain('delivery.releases')
  })

  it('guards Versions on its own permission, not the catalog one', () => {
    // One section, but not one permission: someone who can see products need not be able to see
    // delivery records, and offering the entry anyway leads to a page that refuses them.
    mockFlags['product-management'] = true
    mockClaims.held = new Set(['Permissions.Products.View'])

    const { result } = renderHook(() => useAppMenuItems())

    const keys = keysOf(result.current.menuItems)
    expect(keys).toContain('product.products')
    expect(keys).not.toContain('delivery.versions')
    // The section survives, because Products still passes.
    expect(keys).toContain('product')
  })
})
