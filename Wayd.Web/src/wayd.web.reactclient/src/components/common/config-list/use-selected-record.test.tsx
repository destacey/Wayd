import { renderHook, act } from '@testing-library/react'
import { usePathname, useSearchParams } from 'next/navigation'
import useSelectedRecord, { SELECTED_PARAM } from './use-selected-record'

const mockReplace = jest.fn()
const mockPush = jest.fn()

// The global next/navigation mock hands back a fresh router each call, so
// there is nothing to assert on. Pin one router here instead.
jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockReplace, push: mockPush }),
  usePathname: jest.fn(() => '/settings/ppm/expenditure-categories'),
  useSearchParams: jest.fn(() => new URLSearchParams()),
}))

const mockSearchParams = useSearchParams as jest.Mock
const mockPathname = usePathname as jest.Mock

const withQuery = (query: string) =>
  mockSearchParams.mockReturnValue(new URLSearchParams(query))

describe('useSelectedRecord', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockPathname.mockReturnValue('/settings/ppm/expenditure-categories')
    withQuery('')
  })

  it('reports nothing selected when the param is absent', () => {
    // Arrange / Act
    const { result } = renderHook(() => useSelectedRecord())

    // Assert
    expect(result.current.selectedId).toBeNull()
  })

  it('reads the selected id from the URL', () => {
    // Arrange
    withQuery(`${SELECTED_PARAM}=7`)

    // Act
    const { result } = renderHook(() => useSelectedRecord())

    // Assert
    expect(result.current.selectedId).toBe('7')
  })

  it('writes the selection to the URL', () => {
    // Arrange
    const { result } = renderHook(() => useSelectedRecord())

    // Act
    act(() => result.current.select('12'))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      `/settings/ppm/expenditure-categories?${SELECTED_PARAM}=12`,
      { scroll: false },
    )
  })

  it('replaces rather than pushes, so Back leaves the list', () => {
    // Arrange — stepping through six config rows must not bury the page the
    // user arrived from under six history entries.
    const { result } = renderHook(() => useSelectedRecord())

    // Act
    act(() => result.current.select('3'))
    act(() => result.current.select('5'))
    act(() => result.current.clear())

    // Assert
    expect(mockReplace).toHaveBeenCalledTimes(3)
    expect(mockPush).not.toHaveBeenCalled()
  })

  it('drops the param entirely when cleared', () => {
    // Arrange
    withQuery(`${SELECTED_PARAM}=7`)
    const { result } = renderHook(() => useSelectedRecord())

    // Act
    act(() => result.current.clear())

    // Assert — a bare path, not a dangling `?`
    expect(mockReplace).toHaveBeenCalledWith(
      '/settings/ppm/expenditure-categories',
      { scroll: false },
    )
  })

  it('carries other query state across a selection change', () => {
    // Arrange — the selection is one axis among several; rebuilding the URL
    // from it alone would silently reset the others.
    withQuery('includeArchived=true')
    const { result } = renderHook(() => useSelectedRecord())

    // Act
    act(() => result.current.select('4'))

    // Assert
    const [url] = mockReplace.mock.calls[0]
    const query = new URLSearchParams(url.split('?')[1])
    expect(query.get('includeArchived')).toBe('true')
    expect(query.get(SELECTED_PARAM)).toBe('4')
  })

  it('carries other query state across a clear', () => {
    // Arrange
    withQuery(`includeArchived=true&${SELECTED_PARAM}=4`)
    const { result } = renderHook(() => useSelectedRecord())

    // Act
    act(() => result.current.clear())

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/settings/ppm/expenditure-categories?includeArchived=true',
      { scroll: false },
    )
  })

  it('replaces an existing selection rather than appending one', () => {
    // Arrange
    withQuery(`${SELECTED_PARAM}=4`)
    const { result } = renderHook(() => useSelectedRecord())

    // Act
    act(() => result.current.select('9'))

    // Assert
    const [url] = mockReplace.mock.calls[0]
    expect(url.match(new RegExp(SELECTED_PARAM, 'g'))).toHaveLength(1)
    expect(url).toContain(`${SELECTED_PARAM}=9`)
  })

  it('never scrolls on a selection change', () => {
    // Arrange — without scroll:false the router jumps to top, throwing the
    // user's place in a long config list away on every row they open.
    const { result } = renderHook(() => useSelectedRecord())

    // Act
    act(() => result.current.select('2'))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(expect.any(String), {
      scroll: false,
    })
  })
})
