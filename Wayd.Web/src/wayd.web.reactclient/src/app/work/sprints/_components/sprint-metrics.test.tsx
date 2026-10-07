global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
}

import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import SprintMetrics from './sprint-metrics'
import { IterationState } from '@/src/components/types'
import {
  SprintDetailsDto,
  SprintScopeDto,
  SprintWorkItemMetricsDto,
  SizingMethod,
} from '@/src/services/wayd-api'
import {
  useGetSprintMetricsQuery,
  useGetSprintScopeQuery,
} from '@/src/store/features/work-management/sprints-api'

// Mock dayjs
jest.mock('dayjs', () => {
  const mockDayjs = jest.fn(() => ({
    add: jest.fn().mockReturnThis(),
    endOf: jest.fn().mockReturnThis(),
    startOf: jest.fn().mockReturnThis(),
    format: jest.fn(() => '2025-01-01'),
    toDate: jest.fn(() => new Date()),
  })) as jest.Mock & { extend: jest.Mock }
  mockDayjs.extend = jest.fn()
  return mockDayjs
})

// Mock the API hooks
jest.mock('@/src/store/features/work-management/sprints-api', () => ({
  useGetSprintMetricsQuery: jest.fn(),
  useGetSprintScopeQuery: jest.fn(),
}))

// Mock useTheme
jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({
    token: {
      colorSuccess: '#52c41a',
      colorInfo: '#1890ff',
      colorError: '#ff4d4f',
    },
  }),
}))

const card = (title: string, value: unknown) => (
  <div data-testid={`metric-${title}`}>
    <span>{title}</span>
    <span data-testid={`value-${title}`}>{String(value)}</span>
  </div>
)

// Mock Metrics components; the figures stay real so what is shown can be asserted.
jest.mock('@/src/components/common/metrics', () => ({
  sprintOverviewFigures: jest.requireActual(
    '@/src/components/common/metrics/sprint-overview-figures',
  ).sprintOverviewFigures,
  METRIC_CARD_FLEX: {},
  MetricCard: ({ title, value }: { title: string; value: number }) =>
    card(title, value),
  DaysCountdownMetric: () => <div data-testid="countdown-metric" />,
  CompletionRateMetric: ({
    completed,
    total,
  }: {
    completed: number
    total: number
  }) => card('Completion Rate', `${completed}/${total}`),
  StatusMetric: ({ title, value }: { title: string; value: number }) =>
    card(title, value),
  HealthMetric: ({ title, value }: { title: string; value: number }) =>
    card(title, value),
  CycleTimeMetric: ({ value }: { value: number }) =>
    card('Avg Cycle Time', value),
}))

// Mock IterationHealthIndicator
jest.mock('@/src/components/common/planning', () => ({
  IterationHealthIndicator: ({
    completed,
    total,
  }: {
    completed: number
    total: number
  }) => (
    <div data-testid="iteration-health-indicator">
      {completed}/{total}
    </div>
  ),
}))

const measure = (count: number, estimate: number) => ({ count, estimate })

describe('SprintMetrics', () => {
  const activeSprint: SprintDetailsDto = {
    id: 'sprint-1',
    key: 1,
    name: 'Sprint 1',
    start: '2025-01-01',
    end: '2025-01-14',
    state: { id: IterationState.Active, name: 'Active' },
    team: {
      id: 'team-1',
      key: 1,
      name: 'Team 1',
      code: 'T1',
      type: 'Team',
    },
    overlapsPreviousSprint: false,
    overlapsNextSprint: false,
    canManageSprint: false,
    canStart: false,
    canComplete: false,
    canReopen: false,
  }

  const completedSprint: SprintDetailsDto = {
    ...activeSprint,
    state: { id: IterationState.Completed, name: 'Completed' },
  }

  // What is in the sprint now.
  const metrics: SprintWorkItemMetricsDto = {
    sprintId: 'sprint-1',
    sizingMethod: SizingMethod.Effort,
    totalWorkItems: 10,
    completedWorkItems: 5,
    inProgressWorkItems: 3,
    notStartedWorkItems: 2,
    totalEstimate: 100,
    completedEstimate: 50,
    inProgressEstimate: 30,
    notStartedEstimate: 20,
    unestimatedWorkItems: 1,
    cycleTime: {
      workItemsCount: 4,
      totalCycleTimeDays: 18,
      averageCycleTimeDays: 4.5,
    },
  }

  // Everything that was in the sprint, from history; its commitment point has passed.
  const scope: SprintScopeDto = {
    sprintId: 'sprint-1',
    sizingMethod: SizingMethod.Effort,
    effectiveStart: '2025-01-02T00:00:00Z' as unknown as Date,
    startIsActual: false,
    effectiveEnd: '2025-01-15T00:00:00Z' as unknown as Date,
    endIsActual: false,
    lastDay: '2025-01-14T00:00:00Z' as unknown as Date,
    timeZone: 'UTC',
    hasTeam: true,
    historyIncomplete: false,
    totals: {
      total: measure(12, 120),
      committed: measure(9, 90),
      added: measure(3, 30),
      completed: measure(6, 60),
      removed: measure(1, 5),
      carriedOver: measure(0, 0),
      descoped: measure(1, 10),
      remaining: measure(5, 50),
      completedOfCommitted: measure(4, 45),
      sayDoCount: 4 / 9,
      sayDoEstimate: 0.5,
      unestimated: 0,
    },
    items: [],
  }

  const mockQueries = (
    metricsData: SprintWorkItemMetricsDto | undefined,
    scopeData: SprintScopeDto | undefined,
    isLoading = false,
  ) => {
    ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
      data: metricsData,
      isLoading,
    })
    ;(useGetSprintScopeQuery as jest.Mock).mockReturnValue({
      data: scopeData,
      isLoading,
    })
  }

  const segmentedOptions = (container: HTMLElement) =>
    Array.from(container.querySelectorAll('.ant-segmented-item')).map(
      (option) => option.textContent,
    )

  beforeEach(() => mockQueries(metrics, scope))

  describe('with scope', () => {
    it('shows what the sprint committed to and completed, in its estimate', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(screen.getByTestId('value-Committed')).toHaveTextContent('90')
      expect(screen.getByTestId('value-Added')).toHaveTextContent('30')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('60')
      // Velocity 60 of 90 committed.
      expect(screen.getByTestId('value-Predictability')).toHaveTextContent(
        '66.6',
      )
      expect(screen.getByTestId('value-Say/Do so far')).toHaveTextContent('50')
      expect(screen.getByTestId('value-Descoped')).toHaveTextContent('10')
    })

    it('leaves out the cards the scope makes redundant', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      for (const title of ['Total', 'Completed', 'Remaining', 'WIP'])
        expect(screen.queryByTestId(`metric-${title}`)).not.toBeInTheDocument()
    })

    it('puts progress in the first row and the commitment in the second', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      const rowOf = (title: string) =>
        screen.getByTestId(`metric-${title}`).parentElement
      const titlesIn = (row: HTMLElement | null) =>
        Array.from(row?.children ?? []).map((c) =>
          c.getAttribute('data-testid'),
        )
      expect(titlesIn(rowOf('Velocity'))).toEqual([
        'countdown-metric',
        'metric-Predictability',
        'metric-Velocity',
        'metric-In Progress',
        'metric-Not Started',
        'metric-Avg Cycle Time',
      ])
      expect(titlesIn(rowOf('Committed'))).toEqual([
        'metric-Say/Do so far',
        'metric-Committed',
        'metric-Added',
        'metric-Descoped',
        'metric-Unestimated',
      ])
    })

    it('shows the work in the sprint now while it runs', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(screen.getByTestId('countdown-metric')).toBeInTheDocument()
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('30')
      expect(screen.getByTestId('value-Not Started')).toHaveTextContent('20')
      expect(
        screen.queryByTestId('metric-Carried Over'),
      ).not.toBeInTheDocument()
    })

    it('shows carried-over work on a running sprint once there is some', () => {
      // Arrange
      mockQueries(metrics, {
        ...scope,
        totals: { ...scope.totals, carriedOver: measure(1, 8) },
      })

      // Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(screen.getByTestId('value-Carried Over')).toHaveTextContent('8')
    })

    it('shows how much the work grew by being estimated after it came in', () => {
      // Arrange — 120 points came in, and the work is 130 points now
      mockQueries(metrics, {
        ...scope,
        totals: { ...scope.totals, total: measure(12, 130) },
      })

      // Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(screen.getByTestId('value-Re-estimated')).toHaveTextContent('+10')
    })

    it('hides re-estimation when the estimates did not change', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(
        screen.queryByTestId('metric-Re-estimated'),
      ).not.toBeInTheDocument()
    })

    it('shows the outcome of a completed sprint, not its work in progress', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={completedSprint} />)

      // Assert
      expect(screen.getByTestId('value-Say/Do')).toHaveTextContent('50')
      expect(screen.getByTestId('metric-Carried Over')).toBeInTheDocument()
      expect(screen.queryByTestId('countdown-metric')).not.toBeInTheDocument()
      expect(screen.queryByTestId('metric-In Progress')).not.toBeInTheDocument()
      expect(screen.queryByTestId('metric-Not Started')).not.toBeInTheDocument()
    })

    it('says where the commitment point and end came from', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(
        screen.getByText('Days are counted in the team’s zone, UTC.'),
      ).toBeInTheDocument()
    })
  })

  describe('with incomplete history', () => {
    beforeEach(() =>
      mockQueries(metrics, { ...scope, historyIncomplete: true }),
    )

    it('warns, and measures completion on the items in the sprint now', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(
        screen.getByText('History incomplete — run a full sync'),
      ).toBeInTheDocument()
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('50')
      expect(
        screen.queryByTestId('metric-Predictability'),
      ).not.toBeInTheDocument()
      expect(screen.queryByTestId('metric-Committed')).not.toBeInTheDocument()
      expect(screen.queryByTestId('metric-Descoped')).not.toBeInTheDocument()
      expect(
        screen.queryByTestId('metric-Say/Do so far'),
      ).not.toBeInTheDocument()
    })
  })

  describe('switching to Count', () => {
    it('counts every card with the one switch', async () => {
      // Arrange
      const user = userEvent.setup()
      const { container } = render(<SprintMetrics sprint={activeSprint} />)
      expect(segmentedOptions(container)).toEqual(['Effort', 'Count'])

      // Act
      await user.click(screen.getByText('Count'))

      // Assert
      await waitFor(() => {
        expect(screen.getByTestId('value-Committed')).toHaveTextContent('9')
      })
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('6')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')
      expect(screen.queryByTestId('metric-Unestimated')).not.toBeInTheDocument()
      expect(container.querySelectorAll('.ant-segmented')).toHaveLength(1)
    })

    it('offers only Count for a Count-sized sprint', () => {
      // Arrange
      mockQueries(
        { ...metrics, sizingMethod: SizingMethod.Count },
        { ...scope, sizingMethod: SizingMethod.Count },
      )

      // Act
      const { container } = render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(segmentedOptions(container)).toEqual(['Count'])
      expect(
        container.querySelector('.ant-segmented-disabled'),
      ).toBeInTheDocument()
      expect(screen.getByTestId('value-Committed')).toHaveTextContent('9')
    })
  })

  describe('Average Cycle Time', () => {
    it('renders average cycle time when available', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(screen.getByTestId('value-Avg Cycle Time')).toHaveTextContent(
        '4.5',
      )
    })

    it('does not render cycle time without done work', () => {
      // Arrange
      mockQueries(
        {
          ...metrics,
          cycleTime: {
            workItemsCount: 0,
            totalCycleTimeDays: 0,
            averageCycleTimeDays: undefined,
          },
        },
        scope,
      )

      // Act
      render(<SprintMetrics sprint={activeSprint} />)

      // Assert
      expect(
        screen.queryByTestId('metric-Avg Cycle Time'),
      ).not.toBeInTheDocument()
    })
  })

  it('renders a skeleton while loading', () => {
    // Arrange
    mockQueries(undefined, undefined, true)

    // Act
    const { container } = render(<SprintMetrics sprint={activeSprint} />)

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders the unestimated item count when showing estimates', () => {
    // Arrange / Act
    render(<SprintMetrics sprint={activeSprint} />)

    // Assert
    expect(screen.getByTestId('value-Unestimated')).toHaveTextContent('1')
  })

  describe('health indicator', () => {
    it('measures health on work done against everything in the sprint but descoped work', async () => {
      // Arrange
      const onHealthIndicatorReady = jest.fn()

      // Act
      render(
        <SprintMetrics
          sprint={activeSprint}
          onHealthIndicatorReady={onHealthIndicatorReady}
        />,
      )

      // Assert
      await waitFor(() => expect(onHealthIndicatorReady).toHaveBeenCalled())
      render(onHealthIndicatorReady.mock.calls.at(-1)[0])
      expect(
        screen.getByTestId('iteration-health-indicator'),
      ).toHaveTextContent('60/110')
    })

    it('is not reported while loading', () => {
      // Arrange
      mockQueries(undefined, undefined, true)
      const onHealthIndicatorReady = jest.fn()

      // Act
      render(
        <SprintMetrics
          sprint={activeSprint}
          onHealthIndicatorReady={onHealthIndicatorReady}
        />,
      )

      // Assert
      expect(onHealthIndicatorReady).not.toHaveBeenCalled()
    })
  })
})
