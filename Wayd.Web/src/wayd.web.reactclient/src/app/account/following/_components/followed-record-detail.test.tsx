import { act, fireEvent, render, screen } from '@testing-library/react'
import {
  FollowedRecordDetailDto,
  HealthStatus,
  LifecycleCategory,
} from '@/src/services/wayd-api'
import FollowedRecordDetail from './followed-record-detail'

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

describe('FollowedRecordDetail', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseGetFollowedRecordQuery.mockReturnValue({
      data: detail(),
      isLoading: false,
      isError: false,
    })
    mockUseGetProjectQuery.mockReturnValue({
      data: { stages: [{ name: 'Plan' }, { name: 'Build' }] },
      isLoading: false,
    })
    mockUnfollow.mockResolvedValue({ data: null })
  })

  it('shows where the record stands, who owns it and what has happened since following it', () => {
    render(
      <FollowedRecordDetail
        ownerType="ppm.project"
        ownerId="p-1"
        typeName="Project"
        onUnfollowed={jest.fn()}
      />,
    )

    expect(
      screen.getByRole('heading', { name: 'Compliance dashboard' }),
    ).toBeInTheDocument()
    expect(screen.getByText('COMPDB · Project')).toBeInTheDocument()
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
    render(
      <FollowedRecordDetail
        ownerType="ppm.project"
        ownerId="p-1"
        typeName="Project"
        onUnfollowed={jest.fn()}
      />,
    )

    expect(mockUseGetProjectQuery).toHaveBeenCalledWith('COMPDB')
    expect(screen.getByTestId('stage-timeline')).toHaveTextContent(
      'Plan, Build',
    )
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
  })

  it('shows an objective its progress, and asks for no project', () => {
    mockUseGetFollowedRecordQuery.mockReturnValue({
      data: detail({
        record: {
          ...detail().record,
          ownerType: 'planning.planning-interval-objective',
          key: '7',
          parentKey: '26',
          parentName: 'PI 2026.1',
        },
        progress: 40,
      }),
      isLoading: false,
      isError: false,
    })

    render(
      <FollowedRecordDetail
        ownerType="planning.planning-interval-objective"
        ownerId="o-1"
        typeName="Planning Interval Objective"
        onUnfollowed={jest.fn()}
      />,
    )

    expect(screen.getByRole('progressbar')).toHaveAttribute(
      'aria-valuenow',
      '40',
    )
    expect(mockUseGetProjectQuery).not.toHaveBeenCalled()
  })

  it('shows no progress for a record that tracks none, which the API sends as null', () => {
    mockUseGetFollowedRecordQuery.mockReturnValue({
      data: detail({
        record: { ...detail().record, ownerType: 'ppm.program', key: '7' },
        progress: null as unknown as undefined,
      }),
      isLoading: false,
      isError: false,
    })

    render(
      <FollowedRecordDetail
        ownerType="ppm.program"
        ownerId="pr-1"
        typeName="Program"
        onUnfollowed={jest.fn()}
      />,
    )

    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
    expect(screen.queryByText('Progress')).not.toBeInTheDocument()
  })

  it('links to the record, once', () => {
    render(
      <FollowedRecordDetail
        ownerType="ppm.project"
        ownerId="p-1"
        typeName="Project"
        onUnfollowed={jest.fn()}
      />,
    )

    expect(screen.getByRole('link', { name: 'Open Project' })).toHaveAttribute(
      'href',
      '/ppm/projects/COMPDB',
    )
  })

  it('unfollows and moves on', async () => {
    const onUnfollowed = jest.fn()
    render(
      <FollowedRecordDetail
        ownerType="ppm.project"
        ownerId="p-1"
        typeName="Project"
        onUnfollowed={onUnfollowed}
      />,
    )

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Unfollow' }))
    })

    expect(mockUnfollow).toHaveBeenCalledWith({
      ownerType: 'ppm.project',
      ownerId: 'p-1',
    })
    expect(onUnfollowed).toHaveBeenCalled()
  })

  it('explains a failed unfollow and stays', async () => {
    mockUnfollow.mockResolvedValue({ error: { status: 400, detail: 'Nope.' } })
    const onUnfollowed = jest.fn()
    render(
      <FollowedRecordDetail
        ownerType="ppm.project"
        ownerId="p-1"
        typeName="Project"
        onUnfollowed={onUnfollowed}
      />,
    )

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Unfollow' }))
    })

    expect(mockMessage.error).toHaveBeenCalledWith('Nope.')
    expect(onUnfollowed).not.toHaveBeenCalled()
  })

  it('offers a way back when shown as its own view', () => {
    const onBack = jest.fn()
    render(
      <FollowedRecordDetail
        ownerType="ppm.project"
        ownerId="p-1"
        typeName="Project"
        asView
        onBack={onBack}
        onUnfollowed={jest.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Back to the list/ }))

    expect(onBack).toHaveBeenCalled()
  })

  it('says so when the record has left the list', () => {
    mockUseGetFollowedRecordQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    render(
      <FollowedRecordDetail
        ownerType="ppm.project"
        ownerId="p-1"
        typeName="Project"
        onUnfollowed={jest.fn()}
      />,
    )

    expect(
      screen.getByText('This record is no longer in your list.'),
    ).toBeInTheDocument()
  })
})
