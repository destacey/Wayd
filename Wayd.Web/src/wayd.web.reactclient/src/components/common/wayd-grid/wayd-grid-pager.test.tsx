import { act, render, renderHook, screen } from '@testing-library/react'
import WaydGridPager, { GridPaging, useGridPaging } from './wayd-grid-pager'

const paging = (overrides: Partial<GridPaging> = {}): GridPaging => ({
  page: 1,
  pageSize: 50,
  setPaging: jest.fn(),
  resetPage: jest.fn(),
  keepInRange: jest.fn(),
  ...overrides,
})

describe('useGridPaging', () => {
  it('starts on the first page at the API default size', () => {
    // Act
    const { result } = renderHook(() => useGridPaging())

    // Assert
    expect(result.current.page).toBe(1)
    expect(result.current.pageSize).toBe(50)
  })

  it('moves to the page asked for at the same size', () => {
    // Arrange
    const { result } = renderHook(() => useGridPaging())

    // Act
    act(() => result.current.setPaging(3, 50))

    // Assert
    expect(result.current.page).toBe(3)
    expect(result.current.pageSize).toBe(50)
  })

  it('goes back to the first page when the size changes', () => {
    // Arrange — page 3 of 50 is not the same records as page 3 of 250
    const { result } = renderHook(() => useGridPaging())
    act(() => result.current.setPaging(3, 50))

    // Act
    act(() => result.current.setPaging(3, 250))

    // Assert
    expect(result.current.page).toBe(1)
    expect(result.current.pageSize).toBe(250)
  })

  it('goes back to the first page on reset', () => {
    // Arrange
    const { result } = renderHook(() => useGridPaging())
    act(() => result.current.setPaging(4, 50))

    // Act
    act(() => result.current.resetPage())

    // Assert
    expect(result.current.page).toBe(1)
  })

  it.each([
    [5, 3, 3],
    [5, 0, 1],
    [2, 4, 2],
  ])(
    'from page %i with %i pages left, keeps to page %i',
    (from, totalPages, expected) => {
      // Arrange
      const { result, rerender } = renderHook(
        ({ total }: { total: number | undefined }) => {
          const state = useGridPaging()
          state.keepInRange(total)
          return state
        },
        { initialProps: { total: undefined as number | undefined } },
      )
      act(() => result.current.setPaging(from, 50))

      // Act
      rerender({ total: totalPages })

      // Assert
      expect(result.current.page).toBe(expected)
    },
  )

  it('leaves the page alone before the first response', () => {
    // Arrange
    const { result, rerender } = renderHook(() => {
      const state = useGridPaging()
      state.keepInRange(undefined)
      return state
    })
    act(() => result.current.setPaging(4, 50))

    // Act
    rerender()

    // Assert
    expect(result.current.page).toBe(4)
  })
})

describe('WaydGridPager', () => {
  it('shows the range of the page and the total', () => {
    // Act
    render(
      <WaydGridPager
        paging={paging({ page: 2 })}
        totalCount={312}
        noun="jobs"
      />,
    )

    // Assert
    expect(screen.getByText('51–100 of 312 jobs')).toBeInTheDocument()
  })

  it('says the list is empty rather than showing a range', () => {
    // Act
    render(<WaydGridPager paging={paging()} totalCount={0} noun="rows" />)

    // Assert
    expect(screen.getByText('0 rows')).toBeInTheDocument()
  })

  it('shows nothing until there is a total', () => {
    // Act
    const { container } = render(
      <WaydGridPager paging={paging()} totalCount={undefined} noun="jobs" />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('asks for the page chosen', () => {
    // Arrange
    const setPaging = jest.fn()
    render(
      <WaydGridPager
        paging={paging({ setPaging })}
        totalCount={312}
        noun="jobs"
      />,
    )

    // Act
    act(() => screen.getByTitle('3').click())

    // Assert
    expect(setPaging).toHaveBeenCalledWith(3, 50)
  })
})
