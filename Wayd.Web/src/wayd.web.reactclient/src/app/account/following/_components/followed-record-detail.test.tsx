import { act, fireEvent, render, screen } from '@testing-library/react'
import {
  FollowedRecordDetailDto,
  HealthStatus,
  LifecycleCategory,
} from '@/src/services/wayd-api'
import FollowedRecordDetail from './followed-record-detail'

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

const mockMessage = { error: jest.fn(), success: jest.fn() }
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

const mockUseGetFollowedRecordQuery = jest.fn()
const mockUnfollow = jest.fn()
jest.mock('@/src/store/features/common/follows-api', () => ({
  ...jest.requireActual('@/src/store/features/common/follows-api'),
  useGetFollowedRecordQuery: (...args: unknown[]) =>
    mockUseGetFollowedRecordQuery(...args),
  useUnfollowMutation: () => [mockUnfollow, { isLoading: false }],
}))

const mockUseGetProjectQuery = jest.fn()
jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectQuery: (...args: unknown[]) => mockUseGetProjectQuery(...args),
}))

jest.mock('@/src/app/ppm/_components/stage-timeline', () => ({
  __esModule: true,
  default: ({ stages }: { stages: { name: string }[] }) => (
    <div data-testid="stage-timeline">
      {stages.map((s) => s.name).join(', ')}
    </div>
  ),
}))

jest.mock('@/src/components/common/activities', () => ({
  ActivityLogTimeline: ({ activities }: { activities: unknown[] }) => (
    <div data-testid="activity">{activities.length} changes</div>
  ),
}))

const detail = (
  overrides: Partial<FollowedRecordDetailDto> = {},
): FollowedRecordDetailDto => ({
  record: {
    ownerType: 'ppm.project',
    id: 'p-1',
    key: 'COMPDB',
    name: 'Compliance dashboard',
    statusName: 'Active',
    lifecycle: LifecycleCategory.Active,
    health: HealthStatus.Healthy,
    healthCheck: { id: 'hc-1', status: { id: 1, name: 'Healthy' } },
    followedOn: new Date('2026-10-24T09:00:00Z'),
    lastChangedOn: new Date(Date.now() - 24 * 60 * 60 * 1000),
  },
  people: [
    { role: 'Project Owner', name: 'Sofia Marchetti', employeeId: 'e-1' },
    { role: 'Project Manager', name: 'Aaron Abbott', employeeId: 'e-2' },
    { role: 'Project Manager', name: 'Yusuf Young', employeeId: 'e-3' },
  ],
  start: '2026-10-01',
  end: '2027-01-30',
  changesSinceFollowed: [{} as never],
  hasMoreChanges: false,
  ...overrides,
})

const returns = (data: FollowedRecordDetailDto | undefined, error?: unknown) =>
  mockUseGetFollowedRecordQuery.mockReturnValue({
    data,
    isLoading: false,
    isError: error !== undefined,
    error,
    refetch: jest.fn(),
  })

const renderDetail = (
  props: Partial<React.ComponentProps<typeof FollowedRecordDetail>> = {},
) =>
  render(
    <FollowedRecordDetail
      ownerType="ppm.project"
      ownerId="p-1"
      typeName="Project"
      onUnfollowed={jest.fn()}
      {...props}
    />,
  )

describe('FollowedRecordDetail', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    returns(detail())
    mockUseGetProjectQuery.mockReturnValue({
      data: { stages: [{ name: 'Plan' }, { name: 'Build' }] },
      isLoading: false,
    })
    mockUnfollow.mockResolvedValue({ data: null })
  })

  it('shows where the record stands, who owns it and what has happened since following it', () => {
    // Arrange & Act
    renderDetail()

    // Assert
    expect(
      screen.getByRole('heading', { name: 'Compliance dashboard' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Project')).toBeInTheDocument()
    expect(screen.getByText('COMPDB')).toBeInTheDocument()
    expect(screen.getByText('Healthy')).toBeInTheDocument()
    expect(screen.getByText('Project Owner')).toBeInTheDocument()
    expect(screen.getByText('Sofia Marchetti')).toBeInTheDocument()
    expect(screen.getAllByText('Project Manager')).toHaveLength(1)
    expect(screen.getByText('Aaron Abbott')).toBeInTheDocument()
    expect(screen.getByText('Yusuf Young')).toBeInTheDocument()
    expect(screen.getByText('yesterday')).toBeInTheDocument()
    expect(screen.getByTestId('activity')).toHaveTextContent('1 changes')
  })

  it('shows a project its stages, loaded from the project itself', () => {
    // Arrange & Act
    renderDetail()

    // Assert
    expect(mockUseGetProjectQuery).toHaveBeenCalledWith('COMPDB')
    expect(screen.getByTestId('stage-timeline')).toHaveTextContent(
      'Plan, Build',
    )
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
  })

  it('shows an objective its progress, and asks for no project', () => {
    // Arrange
    returns(
      detail({
        record: {
          ...detail().record,
          ownerType: 'planning.planning-interval-objective',
          key: '7',
          parentKey: '26',
          parentName: 'PI 2026.1',
        },
        progress: 40,
      }),
    )

    // Act
    renderDetail({
      ownerType: 'planning.planning-interval-objective',
      ownerId: 'o-1',
      typeName: 'PI Objective',
    })

    // Assert
    expect(screen.getByRole('progressbar')).toHaveAttribute(
      'aria-valuenow',
      '40',
    )
    expect(mockUseGetProjectQuery).not.toHaveBeenCalled()
  })

  it('shows no progress for a record that tracks none, which the API sends as null', () => {
    // Arrange
    returns(
      detail({
        record: { ...detail().record, ownerType: 'ppm.program', key: '7' },
        progress: null as unknown as undefined,
      }),
    )

    // Act
    renderDetail({
      ownerType: 'ppm.program',
      ownerId: 'pr-1',
      typeName: 'Program',
    })

    // Assert
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
    expect(screen.queryByText('Progress')).not.toBeInTheDocument()
  })

  it('opens the record from its name, with no separate button beside the list', () => {
    // Arrange & Act
    renderDetail()

    // Assert
    expect(screen.getAllByRole('link')).toHaveLength(1)
    expect(
      screen.getByRole('link', { name: 'Compliance dashboard' }),
    ).toHaveAttribute('href', '/ppm/projects/COMPDB')
    expect(
      screen.queryByRole('link', { name: 'Open Project' }),
    ).not.toBeInTheDocument()
  })

  it('links to all the activity when there is more than it shows', () => {
    // Arrange
    returns(detail({ hasMoreChanges: true }))

    // Act
    renderDetail()

    // Assert
    expect(
      screen.getByRole('link', { name: 'See all activity' }),
    ).toHaveAttribute('href', '/ppm/projects/COMPDB')
  })

  it('pins Open and Unfollow on the phone view, Open as a real link', () => {
    // Arrange & Act
    renderDetail({ asView: true, onBack: jest.fn() })

    // Assert
    expect(screen.getByRole('link', { name: 'Open Project' })).toHaveAttribute(
      'href',
      '/ppm/projects/COMPDB',
    )
    expect(screen.getAllByRole('button', { name: 'Unfollow' })).toHaveLength(1)
  })

  it('unfollows and moves on', async () => {
    // Arrange
    const onUnfollowed = jest.fn()
    renderDetail({ onUnfollowed })

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Unfollow' }))
    })

    // Assert
    expect(mockUnfollow).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      ownerId: 'p-1',
    })
    expect(onUnfollowed).toHaveBeenCalled()
  })

  it('explains a failed unfollow and stays', async () => {
    // Arrange
    mockUnfollow.mockResolvedValue({ error: { status: 400, detail: 'Nope.' } })
    const onUnfollowed = jest.fn()
    renderDetail({ onUnfollowed })

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Unfollow' }))
    })

    // Assert
    expect(mockMessage.error).toHaveBeenCalledWith('Nope.')
    expect(onUnfollowed).not.toHaveBeenCalled()
  })

  it('moves focus to the record when asked to', () => {
    // Arrange & Act
    renderDetail({ autoFocus: true })

    // Assert
    expect(
      screen.getByRole('link', { name: 'Compliance dashboard' }),
    ).toHaveFocus()
  })

  it('offers a way back when shown as its own view', () => {
    // Arrange
    const onBack = jest.fn()
    renderDetail({ asView: true, onBack })

    // Act
    fireEvent.click(screen.getByRole('button', { name: /Back to the list/ }))

    // Assert
    expect(onBack).toHaveBeenCalled()
  })

  it('says so when the record has left the list', () => {
    // Arrange
    returns(undefined, { status: 404 })

    // Act
    renderDetail()

    // Assert
    expect(
      screen.getByText('This record is no longer in your list.'),
    ).toBeInTheDocument()
  })

  it('reports a failure to load as a failure, with a retry, not as a record that has gone', () => {
    // Arrange
    returns(undefined, { status: 500 })

    // Act
    renderDetail()

    // Assert
    expect(screen.getByText('Could not load this record.')).toBeInTheDocument()
    expect(
      screen.queryByText('This record is no longer in your list.'),
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Try again' }),
    ).toBeInTheDocument()
  })
})
