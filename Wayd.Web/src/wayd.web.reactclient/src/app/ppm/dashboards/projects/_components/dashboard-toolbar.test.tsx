import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import DashboardToolbar from './dashboard-toolbar'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

const defaultProps = {
  groupBy: 'portfolio' as const,
  onGroupByChange: jest.fn(),
  sortBy: 'attention' as const,
  onSortByChange: jest.fn(),
  search: '',
  onSearchChange: jest.fn(),
  view: 'List' as const,
  onViewChange: jest.fn(),
  shownCount: 4,
  totalCount: 10,
}

describe('DashboardToolbar', () => {
  beforeEach(() => jest.clearAllMocks())

  it('says how many projects are shown of the total', () => {
    // Arrange / Act
    const { rerender } = render(<DashboardToolbar {...defaultProps} />)

    // Assert
    expect(screen.getByText('4 of 10 shown')).toBeInTheDocument()

    rerender(<DashboardToolbar {...defaultProps} shownCount={10} />)
    expect(screen.getByText('10 projects')).toBeInTheDocument()
  })

  it('changes the grouping and the view', async () => {
    // Arrange
    render(<DashboardToolbar {...defaultProps} />)

    // Act
    await userEvent.click(screen.getByText('Health'))
    await userEvent.click(screen.getByTitle('Card view'))

    // Assert
    expect(defaultProps.onGroupByChange).toHaveBeenCalledWith('health')
    expect(defaultProps.onViewChange).toHaveBeenCalledWith('Card')

    await userEvent.click(screen.getByTitle('Timeline'))
    expect(defaultProps.onViewChange).toHaveBeenLastCalledWith('Timeline')
  })

  it('reports search text as it is typed', async () => {
    // Arrange
    render(<DashboardToolbar {...defaultProps} />)

    // Act
    await userEvent.type(screen.getByLabelText('Search projects'), 'b')

    // Assert
    expect(defaultProps.onSearchChange).toHaveBeenCalledWith('b')
  })
})
