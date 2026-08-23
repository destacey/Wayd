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
import { useGetSprintMetricsQuery } from '@/src/store/features/planning/sprints-api'

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
jest.mock('@/src/store/features/planning/sprints-api', () => ({
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

// Mock Metrics components
jest.mock('@/src/components/common/metrics', () => ({
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
    start: new Date('2025-01-01'),
    end: new Date('2025-01-14'),
    state: { id: IterationState.Active, name: 'Active' },
    team: {
      id: 'team-1',
      key: 1,
      name: 'Team 1',
      code: 'T1',
      type: 'Team',
    },
  }

  const mockMetrics: SprintWorkItemMetricsDto = {
    sprintId: 'sprint-1',
    totalWorkItems: 10,
    completedWorkItems: 5,
    inProgressWorkItems: 3,
    notStartedWorkItems: 2,
    totalStoryPoints: 100,
    completedStoryPoints: 50,
    inProgressStoryPoints: 30,
    notStartedStoryPoints: 20,
    missingStoryPointsCount: 1,
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

  describe('Default (Count) mode', () => {
    it('renders all metrics with count by default', () => {
      render(<SprintMetrics sprint={mockSprint} />)

      expect(screen.getByTestId('metric-Completion Rate')).toBeInTheDocument()
      // Note: Our mock for CompletionRateMetric just renders 'completed', which matches the prop we pass.
      // The real component calculates percentage. Here we verify the prop passed is correct (5).
      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent('5')

      expect(screen.getByTestId('metric-Total')).toBeInTheDocument()
      expect(screen.getByTestId('value-Total')).toHaveTextContent('10')

      expect(screen.getByTestId('metric-Velocity')).toBeInTheDocument()
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')

      expect(screen.getByTestId('metric-In Progress')).toBeInTheDocument()
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')

      expect(screen.getByTestId('metric-Not Started')).toBeInTheDocument()
      expect(screen.getByTestId('value-Not Started')).toHaveTextContent('2')
    })
  })

  describe('Story Points mode', () => {
    it('renders all metrics with story points when sizingMethod prop is StoryPoints', () => {
      render(
        <SprintMetrics
          sprint={mockSprint}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      expect(screen.getByTestId('metric-Completion Rate')).toBeInTheDocument()
      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent(
        '50',
      )

      expect(screen.getByTestId('metric-Total')).toBeInTheDocument()
      expect(screen.getByTestId('value-Total')).toHaveTextContent('100')

      expect(screen.getByTestId('metric-Velocity')).toBeInTheDocument()
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('50')

      expect(screen.getByTestId('metric-In Progress')).toBeInTheDocument()
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('30')

      expect(screen.getByTestId('metric-Not Started')).toBeInTheDocument()
      expect(screen.getByTestId('value-Not Started')).toHaveTextContent('20')
    })
  })

  describe('Switching between modes', () => {
    it('switches to story points mode when segmented control is clicked', async () => {
      const user = userEvent.setup()
      render(<SprintMetrics sprint={mockSprint} />)

      // Initially in Count mode, verify count values
      expect(screen.getByTestId('value-Total')).toHaveTextContent('10')

      // Switch to Story Points
      const storyPointsOption = screen.getByText('Story Points')
      await user.click(storyPointsOption)

      await waitFor(() => {
        expect(screen.getByTestId('value-Total')).toHaveTextContent('100')
      })

      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('50')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('30')
      expect(screen.getByTestId('value-Not Started')).toHaveTextContent('20')
    })

    it('switches back to count mode from story points', async () => {
      const user = userEvent.setup()
      render(
        <SprintMetrics
          sprint={mockSprint}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      // Initially in Story Points mode
      expect(screen.getByTestId('value-Total')).toHaveTextContent('100')

      // Switch to Count
      const countOption = screen.getByText('Count')
      await user.click(countOption)

      await waitFor(() => {
        expect(screen.getByTestId('value-Total')).toHaveTextContent('10')
      })

      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')
      expect(screen.getByTestId('value-Not Started')).toHaveTextContent('2')
    })

    it('updates when sizingMethod prop changes', async () => {
      const { rerender } = render(<SprintMetrics sprint={mockSprint} />)

      // Initially in Count mode
      expect(screen.getByTestId('value-Total')).toHaveTextContent('10')

      // Change prop to Story Points
      rerender(
        <SprintMetrics
          sprint={mockSprint}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      await waitFor(() => {
        expect(screen.getByTestId('value-Total')).toHaveTextContent('100')
      })

      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('50')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('30')
      expect(screen.getByTestId('value-Not Started')).toHaveTextContent('20')
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

  describe('Missing SPs', () => {
    it('renders missing SPs in story points mode', () => {
      render(
        <SprintMetrics
          sprint={mockSprint}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )
      expect(screen.getByTestId('metric-Missing SPs')).toBeInTheDocument()
      expect(screen.getByTestId('value-Missing SPs')).toHaveTextContent('1')
    })

    it('does not render missing SPs in count mode', () => {
      render(<SprintMetrics sprint={mockSprint} />)
      expect(screen.queryByTestId('metric-Missing SPs')).not.toBeInTheDocument()
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

      // Should be called initially (in Count mode)
      await waitFor(() => {
        expect(onHealthIndicatorReady).toHaveBeenCalledTimes(1)
      })

      // Switch to Story Points mode
      await user.click(screen.getByText('Story Points'))

      // Should be called again with updated values
      await waitFor(() => {
        expect(onHealthIndicatorReady).toHaveBeenCalledTimes(2)
      })
    })
  })
})

