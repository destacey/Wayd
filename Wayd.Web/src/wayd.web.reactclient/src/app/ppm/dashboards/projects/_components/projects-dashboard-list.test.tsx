import { render, screen, within } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import dayjs from 'dayjs'
import ProjectsDashboardList from './projects-dashboard-list'
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
    summary: '1 unhealthy · 2 overdue tasks',
    projects: [
      project({
        key: 'P1',
        name: 'Project Alpha',
        projectManagers: [me],
        healthCheck: { id: 'hc', status: { id: 3, name: 'Unhealthy' } },
        stages: [
          {
            id: 's1',
            name: 'Design',
            status: { id: 1, name: 'Completed' },
            order: 1,
            progress: 100,
          },
          {
            id: 's2',
            name: 'Build',
            status: { id: 2, name: 'In Progress' },
            order: 2,
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

const today = dayjs('2026-09-24')

const defaultProps = {
  groups,
  planSummaries: {
    'id-P1': { overdue: 2, dueThisWeek: 0, upcoming: 0, totalLeafTasks: 4 },
  },
  employeeId: 'me',
  selectedProjectKey: null,
  onSelectProject: jest.fn(),
  isLoading: false,
  today,
}

describe('ProjectsDashboardList', () => {
  beforeEach(() => jest.clearAllMocks())

  it('renders the group header with its count and summary', () => {
    // Arrange / Act
    render(<ProjectsDashboardList {...defaultProps} />)

    // Assert
    const header = screen.getByRole('button', { expanded: true })
    expect(header).toHaveTextContent('Product Delivery')
    expect(header).toHaveTextContent(
      '2 projects · 1 unhealthy · 2 overdue tasks',
    )
  })

  it('renders each project as a row with its columns', () => {
    // Arrange / Act
    render(<ProjectsDashboardList {...defaultProps} />)

    // Assert
    const row = screen.getByRole('button', { name: 'P1 Project Alpha' })
    expect(row).toHaveTextContent('Unhealthy')
    expect(within(row).getByTestId('stages')).toHaveTextContent('Design,Build')
    expect(row).toHaveTextContent('2 overdue')
    expect(row).toHaveTextContent('Oct 5, 2026')
    expect(row).not.toHaveTextContent('72.5')
    expect(row).toHaveTextContent('PM')

    const quiet = screen.getByRole('button', { name: 'P2 Project Beta' })
    expect(quiet).toHaveTextContent('No lifecycle')
    // Involvement with no project role can only be through an assigned task
    expect(quiet).toHaveTextContent('Task Assignee')
  })

  it('selects a project when its row is clicked and marks the selected row', async () => {
    // Arrange
    const { rerender } = render(<ProjectsDashboardList {...defaultProps} />)

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'P2 Project Beta' }),
    )

    // Assert
    expect(defaultProps.onSelectProject).toHaveBeenCalledWith('P2')

    rerender(
      <ProjectsDashboardList {...defaultProps} selectedProjectKey="P2" />,
    )
    expect(
      screen.getByRole('button', { name: 'P2 Project Beta' }),
    ).toHaveAttribute('aria-pressed', 'true')
  })

  it('collapses and expands a group from its header', async () => {
    // Arrange
    render(<ProjectsDashboardList {...defaultProps} />)

    // Act
    await userEvent.click(screen.getByRole('button', { expanded: true }))

    // Assert
    expect(
      screen.queryByRole('button', { name: 'P1 Project Alpha' }),
    ).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { expanded: false }))
    expect(
      screen.getByRole('button', { name: 'P1 Project Alpha' }),
    ).toBeInTheDocument()
  })

  it('omits the role column when there is no scoped employee', () => {
    // Arrange / Act
    render(<ProjectsDashboardList {...defaultProps} employeeId={null} />)

    // Assert
    expect(screen.queryByText('Role')).not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'P1 Project Alpha' }),
    ).not.toHaveTextContent('PM')
  })

  it('shows an empty state when nothing matches', () => {
    // Arrange / Act
    render(<ProjectsDashboardList {...defaultProps} groups={[]} />)

    // Assert
    expect(
      screen.getByText('No projects match the current scope and filters.'),
    ).toBeInTheDocument()
  })
})
