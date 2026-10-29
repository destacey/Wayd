import { fireEvent, render, screen } from '@testing-library/react'
import RequestFilterBar from './request-filter-bar'
import { DEFAULT_REQUEST_FILTERS } from './request-filters'

const mockSearch = jest.fn()

// The wide layout, where the filters show inline rather than folded into a drawer.
jest.mock('antd', () => {
  const antd = jest.requireActual('antd')
  return {
    ...antd,
    Grid: { ...antd.Grid, useBreakpoint: () => ({ md: true }) },
  }
})

jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useSearchIntakeRequestTagsQuery: (
      args: unknown,
      options: { skip: boolean },
    ) => mockSearch(args, options),
  }),
)

const renderBar = (tagIds: string[]) =>
  render(
    <RequestFilterBar
      filters={{ ...DEFAULT_REQUEST_FILTERS, tagIds }}
      onChange={jest.fn()}
      requests={[]}
      desks={[]}
      channels={[]}
      tagCategories={[]}
      employeeId={null}
      myDeskIds={[]}
    />,
  )

describe('RequestFilterBar', () => {
  beforeEach(() => {
    mockSearch.mockReset().mockReturnValue({ data: undefined })
  })

  it('hides the tag filter while there is no tag to pick and none is applied', () => {
    // Arrange / Act
    renderBar([])

    // Assert
    expect(screen.queryByLabelText('Tags')).not.toBeInTheDocument()
    expect(mockSearch).toHaveBeenCalledWith(
      { isFree: true },
      expect.objectContaining({ skip: true }),
    )
  })

  it('keeps the tag filter, named, while a free tag no request in scope carries is applied', () => {
    // A hidden control would leave the filter applied with no way to clear it but Reset,
    // and an unlooked-up selection would show as an id.
    // Arrange
    mockSearch.mockReturnValue({
      data: [{ id: 'free-q3', name: 'follow up', isFree: true }],
    })

    // Act
    renderBar(['free-q3'])
    fireEvent.mouseDown(screen.getByLabelText('Tags'))

    // Assert
    // Read off the open list: in jsdom the responsive chip row renders no chips.
    expect(screen.getAllByText('follow up (0)').length).toBeGreaterThan(0)
    expect(mockSearch).toHaveBeenCalledWith(
      { isFree: true },
      expect.objectContaining({ skip: false }),
    )
  })
})
