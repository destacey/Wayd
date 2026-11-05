import { renderHook } from '@testing-library/react'
import { ProductTeamLinkDto } from '@/src/services/wayd-api'
import useProductTeamLinkActions from './use-product-team-link-actions'

let mockCanUpdate = true

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => mockCanUpdate }),
}))

// The dialog is never opened here; only whether it is offered.
jest.mock('./unlink-product-team-form', () => () => null)

const link = {
  product: { id: 'web', key: 2, name: 'Storefront Web' },
  productPath: [],
  team: { id: 'payments', key: 7, name: 'Payments', code: 'PAY' },
} as unknown as ProductTeamLinkDto

describe('useProductTeamLinkActions', () => {
  beforeEach(() => {
    mockCanUpdate = true
  })

  it('offers unlinking', () => {
    // Arrange
    const { result } = renderHook(() => useProductTeamLinkActions())

    // Act
    const items = result.current.getActionItems(link)

    // Assert
    expect(items.map((item) => item?.key)).toEqual(['unlink'])
  })

  it('offers nothing without permission to update products', () => {
    // Arrange
    mockCanUpdate = false
    const { result } = renderHook(() => useProductTeamLinkActions())

    // Act
    const items = result.current.getActionItems(link)

    // Assert
    expect(items).toEqual([])
  })
})
