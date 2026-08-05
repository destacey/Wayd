import { renderHook } from '@testing-library/react'
import type { ItemType, MenuItemType } from 'antd/es/menu/interface'

const mockAuth = { employeeId: 'emp-1' as string | null }

jest.mock('../../../components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasClaim: () => true }),
}))

jest.mock('../../../hooks', () => ({
  useFeatureFlag: () => ({ isEnabled: false, isLoading: false }),
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
})
