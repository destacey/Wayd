import { renderHook } from '@testing-library/react'
import { homeContext } from './home-context-test-utils'
import { useHomeContext } from './use-home-context'

const mockLinked = { hasLinkedEmployee: true }
const mockQuery = jest.fn()

jest.mock('@/src/hooks', () => ({
  useLinkedEmployee: () => mockLinked,
}))

jest.mock('@/src/store/features/user-management/me-api', () => ({
  useGetHomeContextQuery: (...args: unknown[]) => mockQuery(...args),
}))

describe('useHomeContext', () => {
  beforeEach(() => {
    mockLinked.hasLinkedEmployee = true
    mockQuery.mockReset()
    mockQuery.mockReturnValue({
      data: homeContext(),
      isLoading: false,
      isError: false,
    })
  })

  it('does not ask for an account with no linked employee', () => {
    // Arrange
    mockLinked.hasLinkedEmployee = false
    mockQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    const { result } = renderHook(() => useHomeContext())

    // Assert
    expect(mockQuery).toHaveBeenCalledWith(
      undefined,
      expect.objectContaining({ skip: true }),
    )
    expect(result.current).toEqual({ context: undefined, isLoading: false })
  })

  it('refetches on mount only when asked to refresh', () => {
    // Arrange, Act
    renderHook(() => useHomeContext({ refresh: true }))
    renderHook(() => useHomeContext())

    // Assert
    expect(mockQuery).toHaveBeenNthCalledWith(1, undefined, {
      skip: false,
      refetchOnMountOrArgChange: true,
    })
    expect(mockQuery).toHaveBeenNthCalledWith(2, undefined, {
      skip: false,
      refetchOnMountOrArgChange: false,
    })
  })

  it('gives no context after a failure, rather than the last answer', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: homeContext({ teams: { teamCount: 1 } }),
      isLoading: false,
      isError: true,
    })

    // Act
    const { result } = renderHook(() => useHomeContext())

    // Assert
    expect(result.current.context).toBeUndefined()
  })
})
