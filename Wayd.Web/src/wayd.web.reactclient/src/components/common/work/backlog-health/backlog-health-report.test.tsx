import { fireEvent, render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import {
  BacklogHealthCheckDto,
  BacklogHealthWorkItemDto,
  TeamBacklogHealthDto,
} from '@/src/services/wayd-api'
import {
  BacklogHealthCheck,
  BacklogHealthOutcome,
} from './backlog-health-formatting'
import { BacklogHealthReportView } from './backlog-health-report'

jest.unmock('dayjs')

// The grid is covered by its own suite; here only which rows reach it matters.
jest.mock('./backlog-health-grid', () => ({
  __esModule: true,
  default: ({ workItems }: { workItems: BacklogHealthWorkItemDto[] }) => (
    <ul data-testid="grid">
      {workItems.map((w) => (
        <li key={w.id}>{w.key}</li>
      ))}
    </ul>
  ),
}))

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamBacklogHealthQuery: jest.fn(),
}))

const graded = (
  id: BacklogHealthCheck,
  name: string,
  grade: string,
  result: Partial<BacklogHealthCheckDto> = {},
): BacklogHealthCheckDto => ({
  check: { id, name },
  outcome: { id: BacklogHealthOutcome.Assessed, name: 'Assessed' },
  grade: { id: 0, name: grade },
  ...result,
})

const workItem = (key: string, flags: BacklogHealthCheck[]) =>
  ({
    id: key,
    key,
    title: key,
    rank: 1,
    flags: flags.map((id) => ({ id, name: BacklogHealthCheck[id] })),
  }) as unknown as BacklogHealthWorkItemDto

const createHealth = (): TeamBacklogHealthDto =>
  ({
    team: { id: 't1', key: 7, name: 'Atlas', code: 'ATL', type: 'Team' },
    thresholds: {},
    lookbackDays: 90,
    from: '2026-06-24',
    to: '2026-09-21',
    totalWorkItems: 2,
    totalStoryPoints: 8,
    proposedWorkItems: 1,
    activeWorkItems: 1,
    itemsCompleted: 30,
    itemsCreated: 28,
    memberCount: 5,
    readinessWindowWorkItems: 10,
    agingWipDays: 12.345,
    checks: [
      graded(BacklogHealthCheck.Runway, 'Runway', 'At Risk', { value: 3.5 }),
      graded(BacklogHealthCheck.NetFlow, 'Net Flow', 'Healthy', {
        value: 0.93,
      }),
      {
        check: { id: BacklogHealthCheck.WipLoad, name: 'WIP Load' },
        outcome: {
          id: BacklogHealthOutcome.NotApplicable,
          name: 'Not Applicable',
        },
      },
      graded(BacklogHealthCheck.Stale, 'Stale', 'Unhealthy', {
        value: 50,
        flagged: 1,
        inScope: 2,
      }),
      graded(BacklogHealthCheck.RankInversion, 'Rank Inversion', 'Healthy', {
        value: 0,
        flagged: 0,
        inScope: 1,
      }),
    ],
    workItems: [
      workItem('CORE-1', [BacklogHealthCheck.Stale]),
      workItem('CORE-2', []),
    ],
  }) as unknown as TeamBacklogHealthDto

const renderView = (health?: TeamBacklogHealthDto) =>
  render(
    <BacklogHealthReportView
      health={health}
      isLoading={false}
      refetch={jest.fn()}
      settings={<span>settings</span>}
    />,
  )

describe('BacklogHealthReportView', () => {
  it('shows the backlog measures with their grades', () => {
    renderView(createHealth())

    expect(screen.getByText('3.5 weeks')).toBeInTheDocument()
    expect(screen.getByText('At Risk')).toBeInTheDocument()
    expect(screen.getByText('0.93 created per completed')).toBeInTheDocument()
    expect(screen.getByText('Not applicable')).toBeInTheDocument()
  })

  it('lists work item checks but not the backlog measures in the table', () => {
    renderView(createHealth())

    const table = screen.getByRole('table')
    expect(table).toHaveTextContent('Stale')
    expect(table).toHaveTextContent('1 of 2 (50%)')
    expect(table).not.toHaveTextContent('Runway')
  })

  it('states the history and the limits the checks used', () => {
    renderView(createHealth())

    expect(
      screen.getByText(
        /30 completed, 28 created\. .*top 10 work items\. Aging beyond 12\.3 days\./,
      ),
    ).toBeInTheDocument()
  })

  it('narrows the grid to the work items a selected check flagged', () => {
    renderView(createHealth())
    expect(screen.getByTestId('grid').children).toHaveLength(2)

    fireEvent.click(screen.getByText('Stale'))

    expect(screen.getByTestId('grid').children).toHaveLength(1)
    expect(screen.getByText('Flagged by Stale')).toBeInTheDocument()
  })

  it('does not select a check that flagged nothing', () => {
    renderView(createHealth())

    fireEvent.click(screen.getByText('Rank Inversion'))

    expect(screen.getByTestId('grid').children).toHaveLength(2)
    expect(screen.getByText('All backlog work items')).toBeInTheDocument()
  })

  it('shows an error instead of the report when it cannot load', () => {
    render(
      <BacklogHealthReportView
        isLoading={false}
        error={new Error('boom')}
        refetch={jest.fn()}
        settings={null}
      />,
    )

    expect(
      screen.getByText('The backlog health report could not be loaded.'),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('grid')).not.toBeInTheDocument()
  })
})
