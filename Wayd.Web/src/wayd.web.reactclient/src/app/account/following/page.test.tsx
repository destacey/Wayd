import { fireEvent, render, screen } from '@testing-library/react'
import {
  FollowCountsDto,
  FollowedRecordDto,
  FollowSort,
  FollowView,
  LifecycleCategory,
  SortDirection,
} from '@/src/services/wayd-api'
import FollowingPage, { parseRecord } from './page'

jest.mock('@/src/components/hoc', () => ({
  requireFeatureFlag: (Page: React.ComponentType) => Page,
}))

const mockUseAuth = jest.fn()
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => mockUseAuth(),
}))

const mockScreens = jest.fn()
jest.mock('antd', () => ({
  ...jest.requireActual('antd'),
  Grid: {
    ...jest.requireActual('antd').Grid,
    useBreakpoint: () => mockScreens(),
  },
}))

const mockPush = jest.fn()
const mockBack = jest.fn()
const mockReplace = jest.fn()
const mockSearchParams = jest.fn()
jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, back: mockBack, replace: mockReplace }),
  usePathname: () => '/account/following',
  useSearchParams: () => mockSearchParams(),
}))

const mockUseCounts = jest.fn()
const mockUseFollows = jest.fn()
jest.mock('@/src/store/features/common/follows-api', () => ({
  ...jest.requireActual('@/src/store/features/common/follows-api'),
  useGetMyFollowCountsQuery: (...args: unknown[]) => mockUseCounts(...args),
  useGetMyFollowsInfiniteQuery: (...args: unknown[]) => mockUseFollows(...args),
}))

jest.mock('./_components/following-rail', () => ({
  __esModule: true,
  default: ({ onChange }: { onChange: (f: { view: FollowView }) => void }) => (
    <div data-testid="rail">
      <button onClick={() => onChange({ view: FollowView.Closed })}>
        Closed view
      </button>
    </div>
  ),
}))
jest.mock('./_components/following-filter-bar', () => ({
  __esModule: true,
  default: () => <div data-testid="filter-bar" />,
}))
jest.mock('./_components/followed-records-list', () => ({
  __esModule: true,
  default: ({
    records,
    onSelect,
    onSortChange,
    onDirectionChange,
  }: {
    records: FollowedRecordDto[]
    onSelect: (r: FollowedRecordDto) => void
    onSortChange: (s: FollowSort) => void
    onDirectionChange: (d: SortDirection) => void
  }) => (
    <div data-testid="list">
      {records.map((r) => (
        <button key={r.id} onClick={() => onSelect(r)}>
          {r.name}
        </button>
      ))}
      <button onClick={() => onSortChange(FollowSort.Followed)}>
        Sort by followed
      </button>
      <button onClick={() => onDirectionChange(SortDirection.Ascending)}>
        Oldest first
      </button>
    </div>
  ),
}))
jest.mock('./_components/followed-record-detail', () => ({
  __esModule: true,
  default: ({
    ownerId,
    asView,
    onBack,
    onUnfollowed,
  }: {
    ownerId: string
    asView?: boolean
    onBack?: () => void
    onUnfollowed: () => void
  }) => (
    <div data-testid="detail">
      {ownerId}
      {asView ? ' (view)' : ''}
      <button onClick={onUnfollowed}>Unfollowed</button>
      {onBack ? <button onClick={onBack}>Back</button> : null}
    </div>
  ),
}))

const record = (n: number): FollowedRecordDto => ({
  ownerType: 'ppm.project',
  id: `id-${n}`,
  key: `P${n}`,
  name: `Project ${n}`,
  statusName: 'Active',
  lifecycle: LifecycleCategory.Active,
  followedOn: new Date('2026-10-01T09:00:00Z'),
})

const counts = (total: number): FollowCountsDto => ({
  total,
  types: [{ ownerType: 'ppm.project', displayName: 'Project', count: total }],
  views: [{ view: FollowView.Closed, count: 0 }],
})

const wide = { xs: true, sm: true, md: true, lg: true, xl: true }
const tablet = { xs: true, sm: true, md: true, lg: true, xl: false }
const phone = { xs: true, sm: true, md: false, lg: false, xl: false }

const givenFollows = (total: number) => {
  mockUseCounts.mockReturnValue({
    data: counts(total),
    isLoading: false,
    isError: false,
  })
  mockUseFollows.mockReturnValue({
    data: {
      pages: [{ items: [record(1), record(2), record(3)], totalCount: total }],
    },
    isLoading: false,
    isError: false,
    hasNextPage: false,
    isFetchingNextPage: false,
    fetchNextPage: jest.fn(),
  })
}

/** The arguments the list query was last asked with. */
const lastListQuery = () => mockUseFollows.mock.calls.at(-1)?.[0]

describe('FollowingPage', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseAuth.mockReturnValue({ user: { employeeId: 'employee-1' } })
    mockSearchParams.mockReturnValue(new URLSearchParams())
    mockScreens.mockReturnValue(wide)
    givenFollows(47)
  })

  it('shows rail, list and detail on a wide screen, with the first record selected', () => {
    // Arrange & Act
    render(<FollowingPage />)

    // Assert
    expect(screen.getByTestId('rail')).toBeInTheDocument()
    expect(screen.queryByTestId('filter-bar')).not.toBeInTheDocument()
    expect(screen.getByTestId('list')).toBeInTheDocument()
    expect(screen.getByTestId('detail')).toHaveTextContent('id-1')
  })

  it('asks for the newest changes first, then for the sort and order chosen', () => {
    // Arrange
    render(<FollowingPage />)
    expect(lastListQuery()).toEqual(
      expect.objectContaining({
        sort: FollowSort.LastChanged,
        direction: SortDirection.Descending,
      }),
    )

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Sort by followed' }))
    fireEvent.click(screen.getByRole('button', { name: 'Oldest first' }))

    // Assert
    expect(lastListQuery()).toEqual(
      expect.objectContaining({
        sort: FollowSort.Followed,
        direction: SortDirection.Ascending,
      }),
    )
  })

  it('shows the record chosen in the list', () => {
    // Arrange
    render(<FollowingPage />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Project 2' }))

    // Assert
    expect(screen.getByTestId('detail')).toHaveTextContent('id-2')
    expect(mockPush).not.toHaveBeenCalled()
  })

  it('moves to the next record after an unfollow, not back to the top', () => {
    // Arrange
    render(<FollowingPage />)
    fireEvent.click(screen.getByRole('button', { name: 'Project 2' }))

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Unfollowed' }))

    // Assert
    expect(screen.getByTestId('detail')).toHaveTextContent('id-3')
  })

  it('moves to the previous record after unfollowing the last one', () => {
    // Arrange
    render(<FollowingPage />)
    fireEvent.click(screen.getByRole('button', { name: 'Project 3' }))

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Unfollowed' }))

    // Assert
    expect(screen.getByTestId('detail')).toHaveTextContent('id-2')
  })

  it('folds the rail into a filter row beside list and detail on a medium screen', () => {
    // Arrange
    mockScreens.mockReturnValue(tablet)

    // Act
    render(<FollowingPage />)

    // Assert
    expect(screen.queryByTestId('rail')).not.toBeInTheDocument()
    expect(screen.getByTestId('filter-bar')).toBeInTheDocument()
    expect(screen.getByTestId('list')).toBeInTheDocument()
    expect(screen.getByTestId('detail')).toBeInTheDocument()
  })

  it('drops the rail and filter row below ten followed records', () => {
    // Arrange
    givenFollows(9)

    // Act
    render(<FollowingPage />)

    // Assert
    expect(screen.queryByTestId('rail')).not.toBeInTheDocument()
    expect(screen.queryByTestId('filter-bar')).not.toBeInTheDocument()
    expect(screen.getByTestId('list')).toBeInTheDocument()
  })

  it('stops filtering once the filter controls are hidden, so nothing narrows the list unseen', () => {
    // Arrange
    const { rerender } = render(<FollowingPage />)
    fireEvent.click(screen.getByRole('button', { name: 'Closed view' }))
    expect(lastListQuery()).toEqual(
      expect.objectContaining({ view: FollowView.Closed }),
    )

    // Act
    givenFollows(9)
    rerender(<FollowingPage />)

    // Assert
    expect(lastListQuery()).toEqual(
      expect.objectContaining({ view: undefined, ownerType: undefined }),
    )
  })

  it('shows only the list on a phone, and opens a record as its own view through the URL', () => {
    // Arrange
    mockScreens.mockReturnValue(phone)
    render(<FollowingPage />)
    expect(screen.queryByTestId('detail')).not.toBeInTheDocument()

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Project 2' }))

    // Assert
    expect(mockPush).toHaveBeenCalledWith(
      `/account/following?record=${encodeURIComponent('ppm.project:id-2')}`,
    )
  })

  it('shows only the detail on a phone when the URL names a record', () => {
    // Arrange
    mockScreens.mockReturnValue(phone)
    mockSearchParams.mockReturnValue(
      new URLSearchParams({ record: 'ppm.project:id-2' }),
    )

    // Act
    render(<FollowingPage />)

    // Assert
    expect(screen.getByTestId('detail')).toHaveTextContent('id-2 (view)')
    expect(screen.queryByTestId('list')).not.toBeInTheDocument()
  })

  it('returns to the list in place, not through history, when the record came from a link', () => {
    // Arrange
    mockScreens.mockReturnValue(phone)
    mockSearchParams.mockReturnValue(
      new URLSearchParams({ record: 'ppm.project:id-2' }),
    )
    render(<FollowingPage />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Back' }))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith('/account/following')
    expect(mockBack).not.toHaveBeenCalled()
  })

  it('goes back through history to a list this page opened the record from', () => {
    // Arrange
    mockScreens.mockReturnValue(phone)
    const { rerender } = render(<FollowingPage />)
    fireEvent.click(screen.getByRole('button', { name: 'Project 2' }))
    mockSearchParams.mockReturnValue(
      new URLSearchParams({ record: 'ppm.project:id-2' }),
    )
    rerender(<FollowingPage />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Back' }))

    // Assert
    expect(mockBack).toHaveBeenCalled()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('ignores a URL that names no followable record', () => {
    // Arrange
    mockScreens.mockReturnValue(phone)
    mockSearchParams.mockReturnValue(
      new URLSearchParams({ record: 'ppm.unknown:id-2' }),
    )

    // Act
    render(<FollowingPage />)

    // Assert
    expect(screen.getByTestId('list')).toBeInTheDocument()
    expect(screen.queryByTestId('detail')).not.toBeInTheDocument()
  })

  it('explains following when nothing is followed', () => {
    // Arrange
    givenFollows(0)

    // Act
    render(<FollowingPage />)

    // Assert
    expect(
      screen.getByText(/You are not following anything yet/),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('list')).not.toBeInTheDocument()
  })

  it('explains that following needs a linked employee', () => {
    // Arrange
    mockUseAuth.mockReturnValue({ user: { employeeId: null } })

    // Act
    render(<FollowingPage />)

    // Assert
    expect(
      screen.getByText(/needs your account to be linked to an employee/),
    ).toBeInTheDocument()
  })
})

describe('parseRecord', () => {
  it.each([
    [null],
    [''],
    ['ppm.project'],
    ['ppm.project:'],
    [':id-1'],
    ['ppm.unknown:id-1'],
  ])('refuses %p', (value) => {
    // Arrange, Act & Assert
    expect(parseRecord(value)).toBeUndefined()
  })

  it('reads a known owner type and id', () => {
    // Arrange, Act & Assert
    expect(parseRecord('planning.planning-interval-objective:o-1')).toEqual({
      ownerType: 'planning.planning-interval-objective',
      ownerId: 'o-1',
    })
  })
})
