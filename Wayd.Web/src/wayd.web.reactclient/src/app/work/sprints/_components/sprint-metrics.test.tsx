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
  SprintWorkItemMetricsDto,
  SizingMethod,
} from '@/src/services/wayd-api'
import { useGetSprintMetricsQuery } from '@/src/store/features/work-management/sprints-api'

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

// Mock Metrics components; sprintMetricValues stays real so the figures shown can be asserted.
jest.mock('@/src/components/common/metrics', () => ({
  sprintMetricValues: jest.requireActual(
    '@/src/components/common/metrics/sprint-metric-values',
  ).sprintMetricValues,
  MetricCard: ({ title, value }: { title: string; value: any }) => (
    <div data-testid={`metric-${title}`}>
      <span>{title}</span>
      <span data-testid={`value-${title}`}>{value}</span>
    </div>
  ),
  DaysCountdownMetric: ({ state }: { state: number }) => (
    <div data-testid="countdown-metric">
      <span>State: {state}</span>
    </div>
  ),
  VelocityMetric: ({ completed }: { completed: number }) => (
    <div data-testid="metric-Velocity">
      <span>Velocity</span>
      <span data-testid="value-Velocity">{completed}</span>
    </div>
  ),
  CompletionRateMetric: ({ completed }: { completed: number }) => (
    <div data-testid="metric-Completion Rate">
      <span>Completion Rate</span>
      <span data-testid="value-Completion Rate">{completed}</span>
    </div>
  ),
  StatusMetric: ({ title, value }: { title: string; value: number }) => (
    <div data-testid={`metric-${title}`}>
      <span>{title}</span>
      <span data-testid={`value-${title}`}>{value}</span>
    </div>
  ),
  HealthMetric: ({ title, value }: { title: string; value: number }) => (
    <div data-testid={`metric-${title}`}>
      <span>{title}</span>
      <span data-testid={`value-${title}`}>{value}</span>
    </div>
  ),
  CycleTimeMetric: ({ value }: { value: number }) => (
    <div data-testid="metric-Avg Cycle Time">
      <span>Avg Cycle Time</span>
      <span data-testid="value-Avg Cycle Time">{value}</span>
    </div>
  ),
}))

// Mock IterationHealthIndicator
jest.mock('@/src/components/common/planning', () => ({
  IterationHealthIndicator: () => (
    <div data-testid="iteration-health-indicator">Health Indicator</div>
  ),
}))

describe('SprintMetrics', () => {
  const mockSprint: SprintDetailsDto = {
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

  const mockMetrics: SprintWorkItemMetricsDto = {
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

  beforeEach(() => {
    ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
      data: mockMetrics,
      isLoading: false,
    })
  })

  const countSizedMetrics: SprintWorkItemMetricsDto = {
    ...mockMetrics,
    sizingMethod: SizingMethod.Count,
    totalEstimate: 10,
    completedEstimate: 5,
    inProgressEstimate: 3,
    notStartedEstimate: 2,
    unestimatedWorkItems: 0,
  }

  const segmentedOptions = (container: HTMLElement) =>
    Array.from(container.querySelectorAll('.ant-segmented-item')).map(
      (option) => option.textContent,
    )

  describe("Default (sprint's sizing method) mode", () => {
    it("renders all metrics in the sprint's sizing method by default", () => {
      // Arrange / Act
      render(<SprintMetrics sprint={mockSprint} />)

      // Assert
      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent(
        '50',
      )
      expect(screen.getByTestId('value-Total')).toHaveTextContent('100')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('50')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('30')
      expect(screen.getByTestId('value-Not Started')).toHaveTextContent('20')
    })

    it('offers the sizing method and Count, with the sizing method selected', () => {
      // Arrange / Act
      const { container } = render(<SprintMetrics sprint={mockSprint} />)

      // Assert
      expect(segmentedOptions(container)).toEqual(['Effort', 'Count'])
      expect(
        container.querySelector('.ant-segmented-item-selected'),
      ).toHaveTextContent('Effort')
      expect(container.querySelector('.ant-segmented-disabled')).toBeNull()
    })
  })

  describe('Switching between modes', () => {
    it('switches to counts when Count is clicked', async () => {
      // Arrange
      const user = userEvent.setup()
      render(<SprintMetrics sprint={mockSprint} />)
      expect(screen.getByTestId('value-Total')).toHaveTextContent('100')

      // Act
      await user.click(screen.getByText('Count'))

      // Assert
      await waitFor(() => {
        expect(screen.getByTestId('value-Total')).toHaveTextContent('10')
      })
      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent('5')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')
      expect(screen.getByTestId('value-Not Started')).toHaveTextContent('2')
    })

    it('switches back to the sizing method from Count', async () => {
      // Arrange
      const user = userEvent.setup()
      render(<SprintMetrics sprint={mockSprint} />)
      await user.click(screen.getByText('Count'))
      await waitFor(() => {
        expect(screen.getByTestId('value-Total')).toHaveTextContent('10')
      })

      // Act
      await user.click(screen.getByText('Effort'))

      // Assert
      await waitFor(() => {
        expect(screen.getByTestId('value-Total')).toHaveTextContent('100')
      })
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('50')
    })
  })

  describe('Count-sized sprint', () => {
    beforeEach(() => {
      ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
        data: countSizedMetrics,
        isLoading: false,
      })
    })

    it('offers only Count, and disables the toggle', () => {
      // Arrange / Act
      const { container } = render(<SprintMetrics sprint={mockSprint} />)

      // Assert
      expect(segmentedOptions(container)).toEqual(['Count'])
      expect(
        container.querySelector('.ant-segmented-disabled'),
      ).toBeInTheDocument()
    })

    it('renders counts', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={mockSprint} />)

      // Assert
      expect(screen.getByTestId('value-Total')).toHaveTextContent('10')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')
    })
  })

  describe('Average Cycle Time', () => {
    it('renders average cycle time when available', () => {
      render(<SprintMetrics sprint={mockSprint} />)
      expect(screen.getByTestId('value-Avg Cycle Time')).toHaveTextContent(
        '4.5',
      )
    })

    it('does not render cycle time when null', () => {
      ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
        data: {
          ...mockMetrics,
          cycleTime: {
            workItemsCount: 0,
            totalCycleTimeDays: 0,
            averageCycleTimeDays: null,
          },
        },
        isLoading: false,
      })
      render(<SprintMetrics sprint={mockSprint} />)
      expect(
        screen.queryByTestId('metric-Avg Cycle Time'),
      ).not.toBeInTheDocument()
    })
  })

  describe('Loading State', () => {
    it('renders skeleton when loading', () => {
      ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
        data: undefined,
        isLoading: true,
      })
      const { container } = render(<SprintMetrics sprint={mockSprint} />)
      expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    })
  })

  describe('WIP', () => {
    it('renders WIP when active', () => {
      render(<SprintMetrics sprint={mockSprint} />)
      expect(screen.getByTestId('metric-WIP')).toBeInTheDocument()
      // WIP is always count of items (3)
      expect(screen.getByTestId('value-WIP')).toHaveTextContent('3')
    })
  })

  describe('Unestimated', () => {
    it('renders the unestimated item count when showing estimates', () => {
      // Arrange / Act
      render(<SprintMetrics sprint={mockSprint} />)

      // Assert
      expect(screen.getByTestId('value-Unestimated')).toHaveTextContent('1')
    })

    it('hides unestimated after switching to Count', async () => {
      // Arrange
      const user = userEvent.setup()
      render(<SprintMetrics sprint={mockSprint} />)

      // Act
      await user.click(screen.getByText('Count'))

      // Assert
      await waitFor(() => {
        expect(
          screen.queryByTestId('metric-Unestimated'),
        ).not.toBeInTheDocument()
      })
    })

    it('does not render unestimated for a Count-sized sprint', () => {
      // Arrange
      ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
        data: { ...countSizedMetrics, unestimatedWorkItems: 3 },
        isLoading: false,
      })

      // Act
      render(<SprintMetrics sprint={mockSprint} />)

      // Assert
      expect(screen.queryByTestId('metric-Unestimated')).not.toBeInTheDocument()
    })
  })

  describe('Health Indicator Callback', () => {
    it('calls onHealthIndicatorReady when metrics are loaded', async () => {
      const onHealthIndicatorReady = jest.fn()
      render(
        <SprintMetrics
          sprint={mockSprint}
          onHealthIndicatorReady={onHealthIndicatorReady}
        />,
      )

      await waitFor(() => {
        expect(onHealthIndicatorReady).toHaveBeenCalled()
      })
    })

    it('does not call onHealthIndicatorReady when loading', () => {
      ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
        data: undefined,
        isLoading: true,
      })

      const onHealthIndicatorReady = jest.fn()
      render(
        <SprintMetrics
          sprint={mockSprint}
          onHealthIndicatorReady={onHealthIndicatorReady}
        />,
      )

      expect(onHealthIndicatorReady).not.toHaveBeenCalled()
    })

    it('updates health indicator when switching modes', async () => {
      const onHealthIndicatorReady = jest.fn()
      const user = userEvent.setup()
      render(
        <SprintMetrics
          sprint={mockSprint}
          onHealthIndicatorReady={onHealthIndicatorReady}
        />,
      )

      // Should be called initially (in the sprint's sizing method)
      await waitFor(() => {
        expect(onHealthIndicatorReady).toHaveBeenCalledTimes(1)
      })

      // Switch to Count mode
      await user.click(screen.getByText('Count'))

      // Should be called again with updated values
      await waitFor(() => {
        expect(onHealthIndicatorReady).toHaveBeenCalledTimes(2)
      })
    })
  })
})
