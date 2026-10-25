import { fireEvent, render, screen } from '@testing-library/react'
import {
  FollowedRecordDto,
  FollowSort,
  HealthStatus,
  LifecycleCategory,
  SortDirection,
} from '@/src/services/wayd-api'
import FollowedRecordsList, {
  FollowedRecordsListProps,
} from './followed-records-list'
import { typeLabel } from '@/src/components/common/record/record-identity'

jest.mock('@/src/store/features/ppm/project-health-checks-api', () => ({
  useGetProjectHealthCheckQuery: () => ({ data: undefined, isLoading: false }),
}))

jest.mock(
  '@/src/store/features/planning/pi-objective-health-checks-api',
  () => ({
    useGetObjectiveHealthCheckQuery: () => ({
      data: undefined,
      isLoading: false,
    }),
  }),
)

const record = (
  n: number,
  overrides: Partial<FollowedRecordDto> = {},
): FollowedRecordDto => ({
  ownerType: 'ppm.project',
  id: `id-${n}`,
  key: `P${n}`,
  name: `Project ${n}`,
  statusName: 'Active',
  lifecycle: LifecycleCategory.Active,
  followedOn: new Date('2026-10-01T09:00:00Z'),
  ...overrides,
})

const props = (
  overrides: Partial<FollowedRecordsListProps> = {},
): FollowedRecordsListProps => ({
  records: [record(1), record(2)],
  totalCount: 2,
  isLoading: false,
  isError: false,
  hasMore: false,
  isLoadingMore: false,
  onLoadMore: jest.fn(),
  search: '',
  onSearchChange: jest.fn(),
  sort: FollowSort.LastChanged,
  onSortChange: jest.fn(),
  direction: SortDirection.Descending,
  onDirectionChange: jest.fn(),
  typeName: (ownerType) => typeLabel(ownerType),
  selectedId: undefined,
  onSelect: jest.fn(),
  ...overrides,
})

const row = (name: RegExp) => screen.getByRole('button', { name })

describe('FollowedRecordsList', () => {
  it('shows each record with its kind, key, parent, status and when it last changed', () => {
    // Arrange
    const objective = record(1, {
      ownerType: 'planning.planning-interval-objective',
      name: 'Launch checkout',
      key: '7',
      parentName: 'PI 2026.1',
      statusName: 'In Progress',
      health: HealthStatus.Unhealthy,
      healthCheck: { id: 'hc-1', status: { id: 3, name: 'Unhealthy' } },
      lastChangedOn: new Date(Date.now() - 2 * 60 * 60 * 1000),
    })

    // Act
    render(
      <FollowedRecordsList
        {...props({ records: [objective], totalCount: 1 })}
      />,
    )

    // Assert
    const item = row(/Launch checkout/)
    expect(item).toHaveTextContent(
      'PI Objective · 7 · PI 2026.1 · changed 2 hours ago',
    )
    expect(item).toHaveTextContent('In Progress')
    expect(screen.getByLabelText('Health: Unhealthy')).toBeInTheDocument()
    expect(screen.getByRole('listitem')).toHaveAttribute('aria-setsize', '1')
  })

  it('names each kind of record as the page does', () => {
    // Arrange
    const typeName = jest.fn(() => 'Gadget')

    // Act
    render(
      <FollowedRecordsList
        {...props({ records: [record(1)], totalCount: 1, typeName })}
      />,
    )

    // Assert
    expect(typeName).toHaveBeenCalledWith('ppm.project')
    expect(row(/Project 1/)).toHaveTextContent('Gadget · P1')
  })

  it('says when a record has no recorded changes', () => {
    // Arrange & Act
    render(
      <FollowedRecordsList
        {...props({ records: [record(1)], totalCount: 1 })}
      />,
    )

    // Assert
    expect(row(/Project 1/)).toHaveTextContent('no changes yet')
  })

  it('selects a record when its row is chosen, and marks the selected row as current', () => {
    // Arrange
    const onSelect = jest.fn()
    render(
      <FollowedRecordsList
        {...props({ onSelect, selectedId: 'ppm.project:id-2' })}
      />,
    )

    // Act
    fireEvent.click(row(/Project 1/))

    // Assert
    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'id-1' }),
    )
    expect(row(/Project 2/)).toHaveAttribute('aria-current', 'true')
    expect(row(/Project 1/)).not.toHaveAttribute('aria-current')
  })

  it('loads the next page as the end of the loaded rows comes into view', () => {
    // Arrange
    const onLoadMore = jest.fn()

    // Act
    render(
      <FollowedRecordsList
        {...props({
          records: Array.from({ length: 12 }, (_, i) => record(i)),
          totalCount: 60,
          hasMore: true,
          onLoadMore,
        })}
      />,
    )

    // Assert
    expect(onLoadMore).toHaveBeenCalled()
  })

  it('does not ask for more when there is nothing more', () => {
    // Arrange
    const onLoadMore = jest.fn()

    // Act
    render(<FollowedRecordsList {...props({ onLoadMore, hasMore: false })} />)

    // Assert
    expect(onLoadMore).not.toHaveBeenCalled()
  })

  it('keeps the loaded rows when the next page fails, and retries only when asked', () => {
    // Arrange
    const onLoadMore = jest.fn()
    render(
      <FollowedRecordsList
        {...props({
          records: Array.from({ length: 12 }, (_, i) => record(i)),
          totalCount: 60,
          hasMore: true,
          isError: true,
          onLoadMore,
        })}
      />,
    )
    expect(onLoadMore).not.toHaveBeenCalled()

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    // Assert
    expect(onLoadMore).toHaveBeenCalledTimes(1)
    expect(row(/Project 0/)).toBeInTheDocument()
    expect(screen.getByText(/Could not load more/)).toBeInTheDocument()
  })

  it('shows an error in place of the list when nothing could be loaded', () => {
    // Arrange & Act
    render(
      <FollowedRecordsList
        {...props({ records: [], totalCount: 0, isError: true })}
      />,
    )

    // Assert
    expect(
      screen.getByText('Could not load what you follow.'),
    ).toBeInTheDocument()
  })

  it('reports how many of the total are loaded', () => {
    // Arrange & Act
    render(<FollowedRecordsList {...props({ totalCount: 60 })} />)

    // Assert
    expect(screen.getByText('2 of 60 shown')).toBeInTheDocument()
  })

  it('passes search text up', () => {
    // Arrange
    const onSearchChange = jest.fn()
    render(<FollowedRecordsList {...props({ onSearchChange })} />)

    // Act
    fireEvent.change(screen.getByLabelText('Filter by name or key'), {
      target: { value: 'pay' },
    })

    // Assert
    expect(onSearchChange).toHaveBeenCalledWith('pay')
  })

  it('passes a new sort up', () => {
    // Arrange
    const onSortChange = jest.fn()
    render(<FollowedRecordsList {...props({ onSortChange })} />)

    // Act
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Sort by' }))
    fireEvent.click(screen.getByText('Date followed'))

    // Assert
    expect(onSortChange).toHaveBeenCalledWith(
      FollowSort.Followed,
      expect.anything(),
    )
  })

  it('flips the order between newest and oldest first', () => {
    // Arrange
    const onDirectionChange = jest.fn()
    const { rerender } = render(
      <FollowedRecordsList {...props({ onDirectionChange })} />,
    )

    // Act
    fireEvent.click(screen.getByRole('button', { name: /^Newest first/ }))
    rerender(
      <FollowedRecordsList
        {...props({ onDirectionChange, direction: SortDirection.Ascending })}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: /^Oldest first/ }))

    // Assert
    expect(onDirectionChange).toHaveBeenNthCalledWith(
      1,
      SortDirection.Ascending,
    )
    expect(onDirectionChange).toHaveBeenNthCalledWith(
      2,
      SortDirection.Descending,
    )
  })

  it('says when nothing matches, with no count beneath it', () => {
    // Arrange & Act
    render(<FollowedRecordsList {...props({ records: [], totalCount: 0 })} />)

    // Assert
    expect(screen.getByText('Nothing matches.')).toBeInTheDocument()
    expect(screen.queryByText(/shown$/)).not.toBeInTheDocument()
  })
})
