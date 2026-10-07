jest.unmock('dayjs')

import { render, screen } from '@testing-library/react'
import SprintCard from './sprint-card'
import { IterationState } from '@/src/components/types'
import {
  SizingMethod,
  SprintMetricsSummary,
  SprintScopeDto,
} from '@/src/services/wayd-api'
import { useGetSprintScopeQuery } from '@/src/store/features/work-management/sprints-api'

jest.mock('@/src/store/features/work-management/sprints-api', () => ({
  useGetSprintScopeQuery: jest.fn(),
}))

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({ token: { colorSuccess: '#52c41a' } }),
}))

// Mock Metrics components; the figures stay real so what is shown can be asserted.
jest.mock('@/src/components/common/metrics', () => ({
  sprintOverviewFigures: jest.requireActual(
    '@/src/components/common/metrics/sprint-overview-figures',
  ).sprintOverviewFigures,
  MetricCard: ({
    title,
    value,
    secondaryValue,
  }: {
    title: string
    value: any
    secondaryValue?: string
  }) => (
    <div data-testid={`metric-${title}`}>
      <span>{title}</span>
      <span data-testid={`value-${title}`}>{value}</span>
      {secondaryValue && (
        <span data-testid={`secondary-${title}`}>{secondaryValue}</span>
      )}
    </div>
  ),
  CompletionRateMetric: ({
    completed,
    tooltip,
  }: {
    completed: number
    tooltip?: string
  }) => (
    <div data-testid="metric-Completion Rate">
      <span>Completion Rate</span>
      <span data-testid="value-Completion Rate">{completed}</span>
      {tooltip && <span data-testid="tooltip-Completion Rate">{tooltip}</span>}
    </div>
  ),
  CycleTimeMetric: ({ value }: { value: number }) => (
    <div data-testid="metric-Cycle Time">
      <span>Cycle Time</span>
      <span data-testid="value-Cycle Time">{value}</span>
    </div>
  ),
}))

// Mock Planning components
jest.mock('@/src/components/common/planning', () => ({
  IterationHealthIndicator: ({
    total,
    completed,
    commitment,
  }: {
    total: number
    completed: number
    commitment?: { committed: number }
  }) => (
    <div data-testid="iteration-health-indicator">
      Health: {completed}/{commitment?.committed ?? total}
    </div>
  ),
  SprintSayDoMetric: () => <div data-testid="metric-Say/Do" />,
  IterationProgressBar: ({
    total,
    completed,
  }: {
    total: number
    completed: number
  }) => (
    <div data-testid="iteration-progress-bar">
      Progress: {completed}/{total}
    </div>
  ),
}))

describe('SprintCard', () => {
  beforeEach(() => {
    // No scope unless a test gives one: the card falls back to the sprint's current items.
    ;(useGetSprintScopeQuery as jest.Mock).mockReturnValue({ data: undefined })
  })

  const mockSprint: SprintMetricsSummary = {
    sprintId: 'sprint-1',
    sprintKey: 101,
    sprintName: 'Sprint 1',
    state: { id: IterationState.Active, name: 'Active' },
    start: '2025-01-01',
    end: '2025-01-14',
    team: {
      id: 'team-1',
      key: 1,
      name: 'Team Alpha',
    },
    sizingMethod: SizingMethod.Effort,
    totalWorkItems: 10,
    totalEstimate: 100,
    completedWorkItems: 5,
    completedEstimate: 50,
    inProgressWorkItems: 3,
    inProgressEstimate: 30,
    notStartedWorkItems: 2,
    notStartedEstimate: 20,
    unestimatedWorkItems: 1,
    cycleTime: {
      workItemsCount: 4,
      totalCycleTimeDays: 18,
      averageCycleTimeDays: 4.5,
    },
  }

  describe('Estimate mode', () => {
    it("renders the sprint's estimates when not by count", () => {
      // Arrange / Act
      render(<SprintCard sprint={mockSprint} byCount={false} />)

      // Assert
      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent(
        '50',
      )
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('50')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('30')
      expect(screen.getByTestId('secondary-In Progress')).toHaveTextContent(
        '20 not started',
      )
    })
  })

  describe('Count mode', () => {
    it('renders work item counts when byCount', () => {
      // Arrange / Act
      render(<SprintCard sprint={mockSprint} byCount />)

      // Assert
      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent('5')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')
      expect(screen.getByTestId('secondary-In Progress')).toHaveTextContent(
        '2 not started',
      )
    })
  })

  describe('Unit tag', () => {
    it.each([
      [SizingMethod.StoryPoints, 'Story Points'],
      [SizingMethod.Effort, 'Effort'],
      [SizingMethod.Size, 'Size'],
      [SizingMethod.Count, 'Count'],
    ])("shows the sprint's sizing method %s as %s", (sizingMethod, label) => {
      // Arrange / Act
      const { container } = render(
        <SprintCard sprint={{ ...mockSprint, sizingMethod }} byCount={false} />,
      )

      // Assert
      expect(container.querySelector('.ant-tag')).toHaveTextContent(
        new RegExp(`^${label}$`),
      )
    })

    it('shows Count when byCount, whatever the sprint is sized in', () => {
      // Arrange / Act
      const { container } = render(<SprintCard sprint={mockSprint} byCount />)

      // Assert
      expect(container.querySelector('.ant-tag')).toHaveTextContent(/^Count$/)
    })
  })

  describe('Header content', () => {
    it('renders team name with correct link', () => {
      render(<SprintCard sprint={mockSprint} byCount />)

      const teamLink = screen.getByRole('link', { name: 'Team Alpha' })
      expect(teamLink).toHaveAttribute('href', '/organizations/teams/1')
    })

    it('renders sprint name with correct link', () => {
      render(<SprintCard sprint={mockSprint} byCount />)

      const sprintLink = screen.getByRole('link', { name: 'Sprint 1' })
      expect(sprintLink).toHaveAttribute('href', '/work/sprints/101')
    })

    it('renders formatted date range', () => {
      render(<SprintCard sprint={mockSprint} byCount />)

      expect(
        screen.getByText(/Jan 1, 2025.*-.*Jan 14, 2025/),
      ).toBeInTheDocument()
    })
  })

  describe('Health indicator', () => {
    it('renders health indicator with correct values in count mode', () => {
      render(<SprintCard sprint={mockSprint} byCount />)

      expect(
        screen.getByTestId('iteration-health-indicator'),
      ).toHaveTextContent('Health: 5/10')
    })

    it('renders health indicator with estimate values when not by count', () => {
      render(<SprintCard sprint={mockSprint} byCount={false} />)

      expect(
        screen.getByTestId('iteration-health-indicator'),
      ).toHaveTextContent('Health: 50/100')
    })
  })

  describe('Progress bar', () => {
    it('renders progress bar for active sprints', () => {
      render(<SprintCard sprint={mockSprint} byCount />)

      expect(screen.getByTestId('iteration-progress-bar')).toBeInTheDocument()
    })

    it('does not render progress bar for future sprints', () => {
      const futureSprint = {
        ...mockSprint,
        state: { id: IterationState.Future, name: 'Future' },
      }

      render(<SprintCard sprint={futureSprint} byCount />)

      expect(
        screen.queryByTestId('iteration-progress-bar'),
      ).not.toBeInTheDocument()
    })
  })

  describe('Future sprint', () => {
    const futureSprint: SprintMetricsSummary = {
      ...mockSprint,
      state: { id: IterationState.Future, name: 'Future' },
    }

    it('only shows Total metric for future sprints', () => {
      render(<SprintCard sprint={futureSprint} byCount />)

      expect(screen.getByTestId('metric-Total')).toBeInTheDocument()
      expect(screen.getByTestId('value-Total')).toHaveTextContent('10')

      // Should not show other metrics
      expect(
        screen.queryByTestId('metric-Completion Rate'),
      ).not.toBeInTheDocument()
      expect(screen.queryByTestId('metric-Velocity')).not.toBeInTheDocument()
      expect(screen.queryByTestId('metric-In Progress')).not.toBeInTheDocument()
      expect(screen.queryByTestId('metric-Cycle Time')).not.toBeInTheDocument()
    })

    it('shows the total estimate for future sprints when not by count', () => {
      render(<SprintCard sprint={futureSprint} byCount={false} />)

      expect(screen.getByTestId('value-Total')).toHaveTextContent('100')
    })
  })

  describe('Active/Completed sprint', () => {
    it('shows all metrics for active sprints', () => {
      render(<SprintCard sprint={mockSprint} byCount />)

      expect(screen.getByTestId('metric-Completion Rate')).toBeInTheDocument()
      expect(screen.getByTestId('metric-Velocity')).toBeInTheDocument()
      expect(screen.getByTestId('metric-In Progress')).toBeInTheDocument()
      expect(screen.getByTestId('metric-Cycle Time')).toBeInTheDocument()
    })

    it('shows all metrics for completed sprints', () => {
      const completedSprint = {
        ...mockSprint,
        state: { id: IterationState.Completed, name: 'Completed' },
      }

      render(<SprintCard sprint={completedSprint} byCount />)

      expect(screen.getByTestId('metric-Completion Rate')).toBeInTheDocument()
      expect(screen.getByTestId('metric-Velocity')).toBeInTheDocument()
      expect(screen.getByTestId('metric-In Progress')).toBeInTheDocument()
      expect(screen.getByTestId('metric-Cycle Time')).toBeInTheDocument()
    })
  })

  describe('with scope', () => {
    const measure = (count: number, estimate: number) => ({ count, estimate })
    const scope = {
      effectiveStart: '2025-01-02T00:00:00Z',
      historyIncomplete: false,
      totals: {
        total: measure(12, 120),
        committed: measure(9, 90),
        added: measure(3, 30),
        completed: measure(6, 60),
        removed: measure(0, 0),
        carriedOver: measure(0, 0),
        descoped: measure(1, 10),
        remaining: measure(5, 50),
        completedOfCommitted: measure(4, 45),
        sayDoCount: 4 / 9,
        sayDoEstimate: 0.5,
        unestimated: 0,
      },
    } as unknown as SprintScopeDto

    beforeEach(() =>
      (useGetSprintScopeQuery as jest.Mock).mockReturnValue({ data: scope }),
    )

    it('shows predictability, velocity and say/do from the scope', () => {
      // Arrange / Act
      render(<SprintCard sprint={mockSprint} byCount={false} />)

      // Assert — velocity 60 of 90 committed
      expect(screen.getByTestId('value-Predictability')).toHaveTextContent(
        '66.6',
      )
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('60')
      expect(screen.getByTestId('metric-Say/Do')).toBeInTheDocument()
      expect(
        screen.queryByTestId('metric-Completion Rate'),
      ).not.toBeInTheDocument()
    })

    it('measures health against the commitment', () => {
      // Arrange / Act
      render(<SprintCard sprint={mockSprint} byCount={false} />)

      // Assert
      expect(
        screen.getByTestId('iteration-health-indicator'),
      ).toHaveTextContent('Health: 60/90')
    })

    it('shows carried-over work instead of work in progress once the sprint has ended', () => {
      // Arrange / Act
      render(
        <SprintCard
          sprint={{
            ...mockSprint,
            state: { id: IterationState.Completed, name: 'Completed' },
          }}
          byCount={false}
        />,
      )

      // Assert
      expect(screen.getByTestId('value-Carried Over')).toHaveTextContent('0')
      expect(screen.queryByTestId('metric-In Progress')).not.toBeInTheDocument()
    })

    it('does not read scope for a future sprint', () => {
      // Arrange / Act
      render(
        <SprintCard
          sprint={{
            ...mockSprint,
            state: { id: IterationState.Future, name: 'Future' },
          }}
          byCount={false}
        />,
      )

      // Assert
      expect(useGetSprintScopeQuery).toHaveBeenCalledWith(101, { skip: true })
    })
  })

  describe('Cycle Time', () => {
    it('renders cycle time when available', () => {
      render(<SprintCard sprint={mockSprint} byCount />)

      expect(screen.getByTestId('value-Cycle Time')).toHaveTextContent('4.5')
    })

    it('renders cycle time as 0 when null', () => {
      const sprintWithNullCycleTime = {
        ...mockSprint,
        cycleTime: {
          workItemsCount: 0,
          totalCycleTimeDays: 0,
          averageCycleTimeDays: undefined,
        },
      }

      render(<SprintCard sprint={sprintWithNullCycleTime} byCount />)

      // Component passes 0 when averageCycleTimeDays is null/undefined
      expect(screen.getByTestId('value-Cycle Time')).toHaveTextContent('0')
    })
  })
})
