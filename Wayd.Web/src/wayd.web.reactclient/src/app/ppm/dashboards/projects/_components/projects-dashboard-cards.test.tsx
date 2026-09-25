import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import dayjs from 'dayjs'
import ProjectsDashboardCards from './projects-dashboard-cards'
import { ProjectGroup } from './dashboard-model'
import { ProjectListDto } from '@/src/services/wayd-api'

// The ending-soon highlight compares dates, which the global dayjs stub cannot do.
jest.unmock('dayjs')

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/app/ppm/projects/_components/project-health-check-tag', () => {
  const MockTag = ({
    healthCheck,
  }: {
    healthCheck?: { status: { name: string } }
  }) => <span>{healthCheck?.status.name ?? 'No health'}</span>
  MockTag.displayName = 'MockProjectHealthCheckTag'
  return MockTag
})

jest.mock('@/src/app/ppm/_components/stage-timeline', () => {
  const MockStages = ({ stages }: { stages: { name: string }[] }) => (
    <span data-testid="stages">{stages.map((s) => s.name).join(',')}</span>
  )
  MockStages.displayName = 'MockStageTimeline'
  return MockStages
})

jest.mock('./team-avatars', () => {
  const MockAvatars = () => <span data-testid="avatars" />
  MockAvatars.displayName = 'MockTeamAvatars'
  return MockAvatars
})

const me = { id: 'me', key: 1, name: 'Me' }

const project = (
  overrides: Partial<ProjectListDto> & { key: string; name: string },
): ProjectListDto =>
  ({
    id: `id-${overrides.key}`,
    status: { id: 2, name: 'Active', lifecycleCategory: 'Active' },
    portfolio: { id: 'port-1', key: 1, name: 'Product Delivery' },
    projectSponsors: [],
    projectOwners: [],
    projectManagers: [],
    projectMembers: [],
    strategicThemes: [],
    stages: [],
    rank: 1,
    canManageProject: false,
    ...overrides,
  }) as ProjectListDto

const groups: ProjectGroup[] = [
  {
    key: 'port-1',
    name: 'Product Delivery',
    summary: '1 unhealthy',
    projects: [
      project({
        key: 'P1',
        name: 'Project Alpha',
        projectManagers: [me],
        healthCheck: { id: 'hc', status: { id: 3, name: 'Unhealthy' } },
        stages: [
          {
            id: 's1',
            name: 'Build',
            status: { id: 2, name: 'In Progress' },
            order: 1,
            progress: 40,
          },
        ],
        end: new Date(2026, 9, 5),
        currentScore: { value: 72.456 } as ProjectListDto['currentScore'],
      }),
      project({ key: 'P2', name: 'Project Beta' }),
    ],
  },
]

const defaultProps = {
  groups,
  planSummaries: {
    'id-P1': { overdue: 2, dueThisWeek: 0, upcoming: 0, totalLeafTasks: 4 },
  },
  employeeId: 'me',
  selectedProjectKey: null,
  onSelectProject: jest.fn(),
  isLoading: false,
  today: dayjs('2026-09-24'),
}

describe('ProjectsDashboardCards', () => {
  beforeEach(() => jest.clearAllMocks())

  it('renders a card per project under the group header', () => {
    // Arrange / Act
    render(<ProjectsDashboardCards {...defaultProps} />)

    // Assert
    expect(screen.getByRole('button', { expanded: true })).toHaveTextContent(
      '2 projects · 1 unhealthy',
    )
    const card = screen.getByRole('button', { name: 'P1 Project Alpha' })
    expect(card).toHaveTextContent('Unhealthy')
    expect(card).toHaveTextContent('P1 · PM')
    expect(card).toHaveTextContent('Build')
    expect(card).toHaveTextContent('2 overdue')
    expect(card).toHaveTextContent('Oct 5, 2026')
    expect(card).toHaveTextContent('Score 72.5')
  })

  it('selects a project when its card is clicked', async () => {
    // Arrange
    render(<ProjectsDashboardCards {...defaultProps} />)

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'P2 Project Beta' }),
    )

    // Assert
    expect(defaultProps.onSelectProject).toHaveBeenCalledWith('P2')
  })

  it('leaves roles off the card when there is no scoped employee', () => {
    // Arrange / Act
    render(<ProjectsDashboardCards {...defaultProps} employeeId={null} />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'P1 Project Alpha' }),
    ).not.toHaveTextContent('PM')
  })
})
