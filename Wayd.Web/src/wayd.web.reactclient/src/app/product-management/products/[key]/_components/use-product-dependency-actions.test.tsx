import { renderHook } from '@testing-library/react'
import {
  DependencyStrength,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import useProductDependencyActions from './use-product-dependency-actions'

let mockCanUpdate = true

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => mockCanUpdate }),
}))

// The dialogs are never opened here; only which ones are offered.
jest.mock(
  '../../_components/change-product-dependency-strength-form',
  () => () => null,
)
jest.mock('../../_components/edit-product-dependency-form', () => () => null)
jest.mock('../../_components/end-product-dependency-form', () => () => null)
jest.mock('../../_components/remove-product-dependency-form', () => () => null)

const link = (
  overrides: Partial<ProductDependencyDto> = {},
): ProductDependencyDto =>
  ({
    id: 'dependency-1',
    product: { id: 'web', key: 2, name: 'Storefront Web' },
    dependsOnProduct: { id: 'identity', key: 3, name: 'Identity Service' },
    strength: DependencyStrength.Hard,
    startsOn: new Date('2026-03-01T00:00:00Z'),
    ...overrides,
  }) as ProductDependencyDto

const keys = (items: { key?: unknown }[]) => items.map((item) => item?.key)

describe('useProductDependencyActions', () => {
  beforeEach(() => {
    mockCanUpdate = true
  })

  it('offers every action on an open dependency', () => {
    // Arrange
    const { result } = renderHook(() => useProductDependencyActions())

    // Act
    const items = result.current.getActionItems(link())

    // Assert
    expect(keys(items as { key?: unknown }[])).toEqual([
      'edit',
      'strength',
      'end',
      'divider',
      'remove',
    ])
  })

  it('offers only editing and removal on an ended dependency', () => {
    // Arrange — ending again or changing strength would rewrite when it held
    const { result } = renderHook(() => useProductDependencyActions())

    // Act
    const items = result.current.getActionItems(
      link({ endsOn: new Date('2026-04-01T00:00:00Z') }),
    )

    // Assert
    expect(keys(items as { key?: unknown }[])).toEqual([
      'edit',
      'divider',
      'remove',
    ])
  })

  it('offers nothing without permission to update products', () => {
    // Arrange
    mockCanUpdate = false
    const { result } = renderHook(() => useProductDependencyActions())

    // Act / Assert
    expect(result.current.getActionItems(link())).toEqual([])
  })
})
