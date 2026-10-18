import { render, screen } from '@testing-library/react'
import KanbanTeamCard from './kanban-team-card'
import {
  DateMeasuredTeamMetricsSummary,
  SizingMethod,
} from '@/src/services/wayd-api'

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({ token: { colorSuccess: '#52c41a' } }),
}))

jest.mock('@/src/components/common/metrics', () => ({
  MetricCard: ({ title, value }: { title: string; value: any }) => (
    <div data-testid={`metric-${title}`}>
      <span data-testid={`value-${title}`}>{value}</span>
    </div>
  ),
  CycleTimeMetric: ({ value }: { value: number }) => (
    <div data-testid="metric-Cycle Time">
      <span data-testid="value-Cycle Time">{value}</span>
    </div>
  ),
}))

jest.mock('@/src/components/common', () => ({
  WaydTooltip: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}))

jest.mock('next/link', () => ({
  __esModule: true,
  default: ({
    children,
    href,
  }: {
    children: React.ReactNode
    href: string
  }) => <a href={href}>{children}</a>,
}))

// Instants reach components as ISO strings from the store, though typed as Date.
const instant = (iso: string) => iso as unknown as Date
const until = instant('2026-09-21T05:00:00Z')

const team = (
  overrides: Partial<DateMeasuredTeamMetricsSummary> = {},
): DateMeasuredTeamMetricsSummary => ({
  team: { id: 'team-1', key: 7, name: 'Flow Team' },
  from: instant('2026-09-07T05:00:00Z'),
  until,
  timeZone: 'America/Chicago',
  sizingMethod: SizingMethod.StoryPoints,
  completedWorkItems: 3,
  completedEstimate: 8,
  inProgressWorkItems: 2,
  inProgressEstimate: 5,
  unestimatedWorkItems: 0,
  cycleTime: {
    workItemsCount: 1,
    totalCycleTimeDays: 2,
    averageCycleTimeDays: 2,
  },
  // A window that has ended reads in progress at its end.
  inProgressAsOf: until,
  isHistoryIncomplete: false,
  ...overrides,
})

describe('KanbanTeamCard', () => {
  it('shows the team as measured by dates, with scope not applicable', () => {
    // Arrange / Act
    render(<KanbanTeamCard team={team()} byCount={false} />)

    // Assert
    expect(screen.getByRole('link', { name: 'Flow Team' })).toHaveAttribute(
      'href',
      '/organizations/teams/7',
    )
    expect(screen.getByText('Measured by dates')).toBeInTheDocument()
    expect(screen.getByTestId('value-Scope')).toHaveTextContent('—')
  })

  it("shows estimates in the team's sizing method", () => {
    // Arrange / Act
    render(<KanbanTeamCard team={team()} byCount={false} />)

    // Assert
    expect(screen.getByText('Story Points')).toBeInTheDocument()
    expect(screen.getByTestId('value-Velocity')).toHaveTextContent('8')
    expect(screen.getByTestId('value-In Progress')).toHaveTextContent('5')
  })

  it('shows item counts when counting', () => {
    // Arrange / Act
    render(<KanbanTeamCard team={team()} byCount />)

    // Assert
    expect(screen.getByText('Count')).toBeInTheDocument()
    expect(screen.getByTestId('value-Velocity')).toHaveTextContent('3')
    expect(screen.getByTestId('value-In Progress')).toHaveTextContent('2')
  })

  it('flags incomplete history for a window that has ended', () => {
    // Arrange / Act
    render(
      <KanbanTeamCard
        team={team({ isHistoryIncomplete: true })}
        byCount={false}
      />,
    )

    // Assert
    expect(screen.getByText('History incomplete')).toBeInTheDocument()
  })

  it("takes a current window from the team's reading, not the iteration's state", () => {
    // Arrange — in progress was read before the window's end, so it is now
    const current = team({
      inProgressAsOf: instant('2026-09-20T12:00:00Z'),
      isHistoryIncomplete: true,
    })

    // Act
    render(<KanbanTeamCard team={current} byCount={false} />)

    // Assert
    expect(screen.queryByText('History incomplete')).toBeNull()
    expect(screen.getByTestId('metric-Velocity')).toBeInTheDocument()
  })

  it('shows only scope for a window that has not begun', () => {
    // Arrange / Act
    render(
      <KanbanTeamCard
        team={team({ inProgressAsOf: undefined })}
        byCount={false}
      />,
    )

    // Assert
    expect(screen.getByTestId('metric-Scope')).toBeInTheDocument()
    expect(screen.queryByTestId('metric-Velocity')).toBeNull()
    expect(screen.queryByTestId('metric-In Progress')).toBeNull()
  })
})
