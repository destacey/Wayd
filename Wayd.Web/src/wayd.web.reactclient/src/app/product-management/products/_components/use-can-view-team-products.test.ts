import { renderHook } from '@testing-library/react'
import useCanViewTeamProducts from './use-can-view-team-products'

let mockFlagEnabled = true
let mockCanViewProducts = true

jest.mock('@/src/hooks', () => ({
  useFeatureFlag: (flag: string) => ({
    isEnabled: flag === 'product-management' && mockFlagEnabled,
  }),
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasPermissionClaim: (permission: string) =>
      permission === 'Permissions.Products.View' && mockCanViewProducts,
  }),
}))

describe('useCanViewTeamProducts', () => {
  beforeEach(() => {
    mockFlagEnabled = true
    mockCanViewProducts = true
  })

  it('shows the section with Product Management on and permission to view products', () => {
    // Act
    const { result } = renderHook(() => useCanViewTeamProducts())

    // Assert
    expect(result.current).toBe(true)
  })

  it('hides it with Product Management off, where the API would answer 404', () => {
    // Arrange
    mockFlagEnabled = false

    // Act
    const { result } = renderHook(() => useCanViewTeamProducts())

    // Assert
    expect(result.current).toBe(false)
  })

  it('hides it without permission to view products', () => {
    // Arrange
    mockCanViewProducts = false

    // Act
    const { result } = renderHook(() => useCanViewTeamProducts())

    // Assert
    expect(result.current).toBe(false)
  })
})
