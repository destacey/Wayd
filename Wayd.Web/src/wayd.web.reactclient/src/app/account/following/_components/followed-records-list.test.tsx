import { fireEvent, render, screen } from '@testing-library/react'
import {
  FollowedRecordDto,
  FollowSort,
  HealthStatus,
  LifecycleCategory,
} from '@/src/services/wayd-api'
import FollowedRecordsList, {
  FollowedRecordsListProps,
} from './followed-records-list'

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
  selectedId: undefined,
  onSelect: jest.fn(),
  ...overrides,
})

describe('FollowedRecordsList', () => {
  it('shows each record with its key, parent, status and when it last changed', () => {
    render(
      <FollowedRecordsList
        {...props({
          records: [
            record(1, {
              name: 'Launch checkout',
              key: '7',
              parentName: 'PI 2026.1',
              statusName: 'In Progress',
              health: HealthStatus.Unhealthy,
              lastChangedOn: new Date(Date.now() - 2 * 60 * 60 * 1000),
            }),
          ],
          totalCount: 1,
        })}
      />,
    )

    const option = screen.getByRole('option', { name: /Launch checkout/ })
    expect(option).toHaveTextContent('7 · PI 2026.1 · changed 2 hours ago')
    expect(option).toHaveTextContent('In Progress')
    expect(screen.getByLabelText('Unhealthy')).toBeInTheDocument()
  })

  it('says when a record has no recorded changes', () => {
    render(
      <FollowedRecordsList
        {...props({ records: [record(1)], totalCount: 1 })}
      />,
    )

    expect(screen.getByRole('option', { name: /Project 1/ })).toHaveTextContent(
      'no changes yet',
    )
  })

  it('selects a record when its row is chosen, and marks the selected row', () => {
    const onSelect = jest.fn()
    render(
      <FollowedRecordsList
        {...props({ onSelect, selectedId: 'ppm.project:id-2' })}
      />,
    )

    fireEvent.click(screen.getByRole('option', { name: /Project 1/ }))

    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'id-1' }),
    )
    expect(screen.getByRole('option', { name: /Project 2/ })).toHaveAttribute(
      'aria-selected',
      'true',
    )
    expect(screen.getByRole('option', { name: /Project 1/ })).toHaveAttribute(
      'aria-selected',
      'false',
    )
  })

  it('loads the next page as the end of the loaded rows comes into view', () => {
    const onLoadMore = jest.fn()
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

    expect(onLoadMore).toHaveBeenCalled()
  })

  it('does not ask for more when there is nothing more', () => {
    const onLoadMore = jest.fn()
    render(<FollowedRecordsList {...props({ onLoadMore, hasMore: false })} />)

    expect(onLoadMore).not.toHaveBeenCalled()
  })

  it('reports how many of the total are loaded', () => {
    render(<FollowedRecordsList {...props({ totalCount: 60 })} />)

    expect(screen.getByText('2 of 60 shown')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('Filter these 60')).toBeInTheDocument()
  })

  it('passes search text and sort changes up', () => {
    const onSearchChange = jest.fn()
    render(<FollowedRecordsList {...props({ onSearchChange })} />)

    fireEvent.change(screen.getByLabelText('Filter by name or key'), {
      target: { value: 'pay' },
    })

    expect(onSearchChange).toHaveBeenCalledWith('pay')
  })

  it('says when nothing matches', () => {
    render(<FollowedRecordsList {...props({ records: [], totalCount: 0 })} />)

    expect(screen.getByText('Nothing matches.')).toBeInTheDocument()
  })
})
