import { fireEvent, render, screen } from '@testing-library/react'
import dayjs from 'dayjs'
import ProjectsDashboardGrid from './projects-dashboard-grid'
import { ProjectListDto } from '@/src/services/wayd-api'

// The ending-soon highlight and the date column compare dates, which the
// global dayjs stub cannot do.
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
  }) => <span>{healthCheck?.status.name}</span>
  MockTag.displayName = 'MockProjectHealthCheckTag'
  return MockTag
})

jest.mock('@/src/app/ppm/_components/stage-timeline', () => {
  const MockStages = ({ stages }: { stages: { name: string }[] }) => (
    <span>{stages.map((s) => s.name).join(',')}</span>
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

const projects = [
  project({
    key: 'P1',
    name: 'Alpha',
    projectManagers: [me],
    projectLifecycle: { id: 'lc', key: 1, name: 'Standard' },
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
  }),
  project({ key: 'P2', name: 'Beta' }),
  project({
    key: 'P3',
    name: 'Gamma',
    portfolio: { id: 'port-2', key: 2, name: 'Internal Ops' },
    healthCheck: { id: 'hc2', status: { id: 1, name: 'Healthy' } },
  }),
]

const props = {
  projects,
  groupBy: 'portfolio' as const,
  onGroupByChange: jest.fn(),
  view: 'List' as const,
  onViewChange: jest.fn(),
  planSummaries: {
    'id-P1': { overdue: 2, dueThisWeek: 0, upcoming: 0, totalLeafTasks: 4 },
  },
  employeeId: 'me',
  selectedProjectKey: null,
  onSelectProject: jest.fn(),
  isLoading: false,
  today: dayjs(new Date(2026, 8, 24)),
  onRefresh: jest.fn(),
}

const groupRows = () =>
  Array.from(document.querySelectorAll('tbody tr[data-group-row]'))

const cells = (columnId: string) =>
  Array.from(
    document.querySelectorAll(`tbody td[data-column-id="${columnId}"]`),
  ).map((c) => c.textContent)

describe('ProjectsDashboardGrid', () => {
  beforeEach(() => jest.clearAllMocks())

  it('groups by the chosen column with a heading that sums up the group', () => {
    // Arrange / Act
    render(<ProjectsDashboardGrid {...props} />)

    // Assert — portfolios in name order, each with its count and summary
    expect(groupRows().map((tr) => tr.textContent)).toEqual([
      'Internal Ops1 project · Nothing needs attention',
      'Product Delivery2 projects · 1 unhealthy · 2 overdue tasks',
    ])
    // The grouped column has become the headings
    expect(cells('portfolio')).toHaveLength(0)
  })

  it('puts the worst health first within a group', () => {
    // Arrange / Act
    render(<ProjectsDashboardGrid {...props} />)

    // Assert — Gamma sits alone in its group; Alpha (unhealthy) before Beta
    expect(cells('key')).toEqual(['P3', 'P1', 'P2'])
    expect(cells('health')).toEqual(['Healthy', 'Unhealthy', 'Not reported'])
  })

  it('regroups when the choice changes', () => {
    // Arrange / Act
    render(<ProjectsDashboardGrid {...props} groupBy="health" />)

    // Assert — worst first, and the health column leaves the rows
    expect(
      groupRows().map((tr) => tr.textContent?.split('1 project')[0]),
    ).toEqual(['Unhealthy', 'Healthy', 'Not reported'])
    expect(cells('health')).toHaveLength(0)
  })

  it('shows the scoped employee’s roles and drops the column without one', () => {
    // Arrange / Act
    const { rerender } = render(<ProjectsDashboardGrid {...props} />)

    // Assert
    expect(cells('role')).toEqual(['Task Assignee', 'PM', 'Task Assignee'])

    rerender(<ProjectsDashboardGrid {...props} employeeId={null} />)
    expect(cells('role')).toHaveLength(0)
  })

  it('carries Group by and the view switch in the grid toolbar', () => {
    // Arrange / Act
    render(<ProjectsDashboardGrid {...props} />)

    // Assert
    // The column header says Health too; the segmented option carries a title.
    fireEvent.click(screen.getByTitle('Health'))
    expect(props.onGroupByChange).toHaveBeenCalledWith('health')
    fireEvent.click(screen.getByTitle('Timeline'))
    expect(props.onViewChange).toHaveBeenCalledWith('Timeline')
    expect(screen.getByPlaceholderText('Search')).toBeInTheDocument()
  })

  it('links straight to the plan of a project that has a lifecycle', () => {
    // Arrange / Act
    render(<ProjectsDashboardGrid {...props} />)

    // Assert — Alpha has a lifecycle, Beta does not
    expect(
      screen.getByRole('link', { name: 'Open plan for P1' }),
    ).toHaveAttribute('href', '/ppm/projects/P1?section=plan')
    expect(
      screen.queryByRole('link', { name: 'Open plan for P2' }),
    ).not.toBeInTheDocument()
  })

  it('keeps Start out of the default columns but in the chooser', () => {
    // Arrange / Act
    render(<ProjectsDashboardGrid {...props} />)

    // Assert — no Start cells rendered; End still is
    expect(cells('start')).toHaveLength(0)
    expect(cells('end')).toHaveLength(3)
  })

  it('opens a project when its row is activated', () => {
    // Arrange
    render(<ProjectsDashboardGrid {...props} />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'P1 Alpha' }))

    // Assert
    expect(props.onSelectProject).toHaveBeenCalledWith('P1')
  })
})
