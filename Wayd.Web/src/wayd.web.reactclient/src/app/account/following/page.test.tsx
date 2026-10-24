import { fireEvent, render, screen } from '@testing-library/react'
import {
  FollowCountsDto,
  FollowedRecordDto,
  FollowView,
  LifecycleCategory,
} from '@/src/services/wayd-api'
import FollowingPage from './page'

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
const mockSearchParams = jest.fn()
jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, back: mockBack }),
  usePathname: () => '/account/following',
  useSearchParams: () => mockSearchParams(),
}))

const mockUseCounts = jest.fn()
const mockUseFollows = jest.fn()
jest.mock('@/src/store/features/common/follows-api', () => ({
  useGetMyFollowCountsQuery: (...args: unknown[]) => mockUseCounts(...args),
  useGetMyFollowsInfiniteQuery: (...args: unknown[]) => mockUseFollows(...args),
}))

jest.mock('./_components/following-rail', () => ({
  __esModule: true,
  default: () => <nav data-testid="rail" />,
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
  }: {
    records: FollowedRecordDto[]
    onSelect: (r: FollowedRecordDto) => void
  }) => (
    <div data-testid="list">
      {records.map((r) => (
        <button key={r.id} onClick={() => onSelect(r)}>
          {r.name}
        </button>
      ))}
    </div>
  ),
}))
jest.mock('./_components/followed-record-detail', () => ({
  __esModule: true,
  default: ({ ownerId, asView }: { ownerId: string; asView?: boolean }) => (
    <div data-testid="detail">
      {ownerId}
      {asView ? ' (view)' : ''}
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
    data: { pages: [{ items: [record(1), record(2)], totalCount: total }] },
    isLoading: false,
    isError: false,
    hasNextPage: false,
    isFetchingNextPage: false,
    fetchNextPage: jest.fn(),
  })
}

describe('FollowingPage', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseAuth.mockReturnValue({ user: { employeeId: 'employee-1' } })
    mockSearchParams.mockReturnValue(new URLSearchParams())
    mockScreens.mockReturnValue(wide)
    givenFollows(47)
  })

  it('shows rail, list and detail on a wide screen, with the first record selected', () => {
    render(<FollowingPage />)

    expect(screen.getByTestId('rail')).toBeInTheDocument()
    expect(screen.queryByTestId('filter-bar')).not.toBeInTheDocument()
    expect(screen.getByTestId('list')).toBeInTheDocument()
    expect(screen.getByTestId('detail')).toHaveTextContent('id-1')
  })

  it('shows the record chosen in the list', () => {
    render(<FollowingPage />)

    fireEvent.click(screen.getByRole('button', { name: 'Project 2' }))

    expect(screen.getByTestId('detail')).toHaveTextContent('id-2')
    expect(mockPush).not.toHaveBeenCalled()
  })

  it('folds the rail into a filter row beside list and detail on a medium screen', () => {
    mockScreens.mockReturnValue(tablet)

    render(<FollowingPage />)

    expect(screen.queryByTestId('rail')).not.toBeInTheDocument()
    expect(screen.getByTestId('filter-bar')).toBeInTheDocument()
    expect(screen.getByTestId('list')).toBeInTheDocument()
    expect(screen.getByTestId('detail')).toBeInTheDocument()
  })

  it('drops the rail and filter row below ten followed records', () => {
    givenFollows(9)

    render(<FollowingPage />)

    expect(screen.queryByTestId('rail')).not.toBeInTheDocument()
    expect(screen.queryByTestId('filter-bar')).not.toBeInTheDocument()
    expect(screen.getByTestId('list')).toBeInTheDocument()
  })

  it('shows only the list on a phone, and opens a record as its own view through the URL', () => {
    mockScreens.mockReturnValue(phone)

    render(<FollowingPage />)

    expect(screen.getByTestId('list')).toBeInTheDocument()
    expect(screen.queryByTestId('detail')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Project 2' }))

    expect(mockPush).toHaveBeenCalledWith(
      `/account/following?record=${encodeURIComponent('ppm.project:id-2')}`,
    )
  })

  it('shows only the detail on a phone when the URL names a record', () => {
    mockScreens.mockReturnValue(phone)
    mockSearchParams.mockReturnValue(
      new URLSearchParams({ record: 'ppm.project:id-2' }),
    )

    render(<FollowingPage />)

    expect(screen.getByTestId('detail')).toHaveTextContent('id-2 (view)')
    expect(screen.queryByTestId('list')).not.toBeInTheDocument()
  })

  it('explains following when nothing is followed', () => {
    givenFollows(0)

    render(<FollowingPage />)

    expect(
      screen.getByText(/You are not following anything yet/),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('list')).not.toBeInTheDocument()
  })

  it('explains that following needs a linked employee', () => {
    mockUseAuth.mockReturnValue({ user: { employeeId: null } })

    render(<FollowingPage />)

    expect(
      screen.getByText(/needs your account to be linked to an employee/),
    ).toBeInTheDocument()
  })
})
