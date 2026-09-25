import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import dayjs from 'dayjs'
import ProjectsDashboardTimeline, {
  buildTimelineModel,
} from './projects-dashboard-timeline'
import { ProjectGroup } from './dashboard-model'
import { ProjectListDto, ProjectStageListDto } from '@/src/services/wayd-api'

// Building the model does date arithmetic the global dayjs stub cannot do.
jest.unmock('dayjs')

const captured: { props: Record<string, unknown> | null } = { props: null }

jest.mock('@/src/components/common/timeline', () => ({
  WaydTimeline: (props: Record<string, unknown>) => {
    captured.props = props
    const items = props.items as {
      id: string
      label: string
      data: { projectKey: string }
    }[]
    const onItemClick = props.onItemClick as (item: unknown) => void
    return (
      <div data-testid="timeline">
        {items.map((item) => (
          <button key={item.id} type="button" onClick={() => onItemClick(item)}>
            {item.label}
          </button>
        ))}
      </div>
    )
  },
}))

const token = {
  colorSuccess: 'green',
  colorWarning: 'orange',
  colorError: 'red',
  colorTextDisabled: 'grey',
  colorPrimary: 'blue',
  colorPrimaryBorder: 'lightblue',
  colorFillSecondary: 'silver',
  colorTextQuaternary: 'gainsboro',
} as never

const stage = (
  id: string,
  name: string,
  status: string,
  order: number,
  start?: Date,
  end?: Date,
): ProjectStageListDto => ({
  id,
  name,
  status: { id: 1, name: status },
  order,
  progress: 40,
  start,
  end,
})

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

const today = dayjs(new Date(2026, 8, 24))

const groups: ProjectGroup[] = [
  {
    key: 'port-1',
    name: 'Product Delivery',
    summary: '',
    projects: [
      project({
        key: 'P1',
        name: 'Alpha',
        healthCheck: { id: 'hc', status: { id: 3, name: 'Unhealthy' } },
        start: new Date(2026, 4, 1),
        end: new Date(2026, 11, 18),
        stages: [
          stage(
            's1',
            'Design',
            'Completed',
            1,
            new Date(2026, 4, 1),
            new Date(2026, 6, 1),
          ),
          stage(
            's2',
            'Build',
            'In Progress',
            2,
            new Date(2026, 5, 15),
            new Date(2026, 9, 1),
          ),
          stage('s3', 'Close', 'Not Started', 3),
        ],
      }),
      project({ key: 'P2', name: 'Beta' }),
    ],
  },
]

describe('buildTimelineModel', () => {
  it('makes a heading row per group and a child row per project', () => {
    // Act
    const model = buildTimelineModel(groups, today, token)

    // Assert
    expect(model.groups.map((g) => [g.id, g.parentId])).toEqual([
      ['group:port-1', undefined],
      ['project:id-P1', 'group:port-1'],
      ['project:id-P2', 'group:port-1'],
    ])
  })

  it('draws the project bar in health colour first, then each dated stage in status colour', () => {
    // Act
    const model = buildTimelineModel(groups, today, token)

    // Assert — the undated Close stage is left out, the project bar sorts first
    expect(model.items.map((i) => [i.id, i.groupId, i.color, i.order])).toEqual(
      [
        ['project:id-P1', 'project:id-P1', 'red', -1],
        ['stage:s1', 'project:id-P1', 'lightblue', 1],
        ['stage:s2', 'project:id-P1', 'blue', 2],
      ],
    )
    expect(model.items[2].tooltip).toContain('In Progress · 40%')
    expect(model.items[0].pinToTop).toBe(true)
    expect(model.items[1].pinToTop).toBeUndefined()
  })

  it('counts projects with nothing to draw and bounds the axis by the data', () => {
    // Act
    const model = buildTimelineModel(groups, today, token)

    // Assert
    expect(model.undatedCount).toBe(1)
    expect(dayjs(model.minDate).format('YYYY-MM-DD')).toBe('2026-04-01')
    expect(dayjs(model.maxDate).format('YYYY-MM-DD')).toBe('2027-01-18')
  })

  it('anchors an empty axis on today', () => {
    // Act
    const model = buildTimelineModel([], today, token)

    // Assert
    expect(dayjs(model.minDate).format('YYYY-MM-DD')).toBe('2026-08-24')
    expect(model.undatedCount).toBe(0)
  })
})

describe('ProjectsDashboardTimeline', () => {
  const props = {
    groups,
    planSummaries: {},
    employeeId: null,
    selectedProjectKey: null,
    onSelectProject: jest.fn(),
    isLoading: false,
    today,
  }

  beforeEach(() => {
    jest.clearAllMocks()
    captured.props = null
  })

  it('hands the timeline the rows and bars and opens a project from any bar', async () => {
    // Arrange
    render(<ProjectsDashboardTimeline {...props} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Build' }))

    // Assert
    expect(captured.props?.variant).toBe('timeline')
    expect(captured.props?.editable).toBe(false)
    expect(props.onSelectProject).toHaveBeenCalledWith('P1')
  })

  it('shows the empty state instead of an empty chart', () => {
    // Arrange / Act
    render(<ProjectsDashboardTimeline {...props} groups={[]} />)

    // Assert
    expect(
      screen.getByText('No projects match the current scope and filters.'),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('timeline')).not.toBeInTheDocument()
  })
})
