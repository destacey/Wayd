import { render, screen } from '@testing-library/react'
import SprintCard from './sprint-card'
import { IterationState } from '@/src/components/types'
import {
  SizingMethod,
  SprintMetricsSummary,
  TeamOperatingModelDetailsDto,
  Methodology,
} from '@/src/services/wayd-api'

// Mock Metrics components
jest.mock('@/src/components/common/metrics', () => ({
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
  VelocityMetric: ({
    completed,
    tooltip,
  }: {
    completed: number
    tooltip?: string
  }) => (
    <div data-testid="metric-Velocity">
      <span>Velocity</span>
      <span data-testid="value-Velocity">{completed}</span>
      {tooltip && <span data-testid="tooltip-Velocity">{tooltip}</span>}
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
  }: {
    total: number
    completed: number
  }) => (
    <div data-testid="iteration-health-indicator">
      Health: {completed}/{total}
    </div>
  ),
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
  const mockSprint: SprintMetricsSummary = {
    sprintId: 'sprint-1',
    sprintKey: 101,
    sprintName: 'Sprint 1',
    state: { id: IterationState.Active, name: 'Active' },
    start: new Date('2025-01-01T09:00:00'),
    end: new Date('2025-01-14T17:00:00'),
    team: {
      id: 'team-1',
      key: 1,
      name: 'Team Alpha',
    },
    totalWorkItems: 10,
    totalStoryPoints: 100,
    completedWorkItems: 5,
    completedStoryPoints: 50,
    inProgressWorkItems: 3,
    inProgressStoryPoints: 30,
    notStartedWorkItems: 2,
    notStartedStoryPoints: 20,
    missingStoryPointsCount: 1,
    cycleTime: {
      workItemsCount: 4,
      totalCycleTimeDays: 18,
      averageCycleTimeDays: 4.5,
    },
  }

  const mockOperatingModelStoryPoints: TeamOperatingModelDetailsDto = {
    id: 'om-1',
    teamId: 'team-1',
    start: new Date('2024-01-01'),
    methodology: Methodology.Scrum,
    sizingMethod: SizingMethod.StoryPoints,
    isCurrent: true,
  }

  const mockOperatingModelCount: TeamOperatingModelDetailsDto = {
    id: 'om-2',
    teamId: 'team-1',
    start: new Date('2024-01-01'),
    methodology: Methodology.Kanban,
    sizingMethod: SizingMethod.Count,
    isCurrent: true,
  }

  describe('Count mode', () => {
    it('renders metrics with work item counts when sizingMethod is Count', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent('5')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')
      expect(screen.getByTestId('secondary-In Progress')).toHaveTextContent(
        '2 not started',
      )
    })

    it('renders metrics with work item counts when operatingModel uses Count', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelCount}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      // Should fallback to count since operatingModel uses Count
      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent('5')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')
    })
  })

  describe('Story Points mode', () => {
    it('renders metrics with story points when both sizingMethod and operatingModel support it', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

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

  describe('Count-based sizing tag', () => {
    it('shows count-based sizing tag when sizingMethod is StoryPoints but operatingModel uses Count', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelCount}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      expect(screen.getByText('Count-based Metrics')).toBeInTheDocument()
    })

    it('does not show count-based sizing tag when sizingMethod is Count', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelCount}
          sizingMethod={SizingMethod.Count}
        />,
      )

      expect(screen.queryByText('Count-based Metrics')).not.toBeInTheDocument()
    })

    it('does not show count-based sizing tag when operatingModel supports StoryPoints', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      expect(screen.queryByText('Count-based Metrics')).not.toBeInTheDocument()
    })
  })

  describe('Header content', () => {
    it('renders team name with correct link', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

      const teamLink = screen.getByRole('link', { name: 'Team Alpha' })
      expect(teamLink).toHaveAttribute('href', '/organizations/teams/1')
    })

    it('renders sprint name with correct link', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

      const sprintLink = screen.getByRole('link', { name: 'Sprint 1' })
      expect(sprintLink).toHaveAttribute('href', '/planning/sprints/101')
    })

    it('renders formatted date range', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

      // dayjs is mocked globally, check that dates are rendered
      expect(
        screen.getByText(/Jan 1, 2025.*-.*Jan 14, 2025/),
      ).toBeInTheDocument()
    })
  })

  describe('Health indicator', () => {
    it('renders health indicator with correct values in count mode', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

      expect(screen.getByTestId('iteration-health-indicator')).toHaveTextContent(
        'Health: 5/10',
      )
    })

    it('renders health indicator with story point values when applicable', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      expect(screen.getByTestId('iteration-health-indicator')).toHaveTextContent(
        'Health: 50/100',
      )
    })
  })

  describe('Progress bar', () => {
    it('renders progress bar for active sprints', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

      expect(screen.getByTestId('iteration-progress-bar')).toBeInTheDocument()
    })

    it('does not render progress bar for future sprints', () => {
      const futureSprint = {
        ...mockSprint,
        state: { id: IterationState.Future, name: 'Future' },
      }

      render(
        <SprintCard
          sprint={futureSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

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
      render(
        <SprintCard
          sprint={futureSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

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

    it('shows Total with story points for future sprints in story points mode', () => {
      render(
        <SprintCard
          sprint={futureSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      expect(screen.getByTestId('value-Total')).toHaveTextContent('100')
    })
  })

  describe('Active/Completed sprint', () => {
    it('shows all metrics for active sprints', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

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

      render(
        <SprintCard
          sprint={completedSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

      expect(screen.getByTestId('metric-Completion Rate')).toBeInTheDocument()
      expect(screen.getByTestId('metric-Velocity')).toBeInTheDocument()
      expect(screen.getByTestId('metric-In Progress')).toBeInTheDocument()
      expect(screen.getByTestId('metric-Cycle Time')).toBeInTheDocument()
    })
  })

  describe('Cycle Time', () => {
    it('renders cycle time when available', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

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

      render(
        <SprintCard
          sprint={sprintWithNullCycleTime}
          operatingModel={mockOperatingModelStoryPoints}
          sizingMethod={SizingMethod.Count}
        />,
      )

      // Component passes 0 when averageCycleTimeDays is null/undefined
      expect(screen.getByTestId('value-Cycle Time')).toHaveTextContent('0')
    })
  })

  describe('Undefined operatingModel', () => {
    it('renders with count values when operatingModel is undefined', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={undefined}
          sizingMethod={SizingMethod.Count}
        />,
      )

      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent('5')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')
    })

    it('falls back to count values when operatingModel is undefined and sizingMethod is StoryPoints', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={undefined}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      // Should use count values since operatingModel is undefined
      expect(screen.getByTestId('value-Completion Rate')).toHaveTextContent('5')
      expect(screen.getByTestId('value-Velocity')).toHaveTextContent('5')
      expect(screen.getByTestId('value-In Progress')).toHaveTextContent('3')
    })

    it('shows count-based sizing tag when operatingModel is undefined and sizingMethod is StoryPoints', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={undefined}
          sizingMethod={SizingMethod.StoryPoints}
        />,
      )

      expect(screen.getByText('Count-based Metrics')).toBeInTheDocument()
    })

    it('does not show count-based sizing tag when operatingModel is undefined and sizingMethod is Count', () => {
      render(
        <SprintCard
          sprint={mockSprint}
          operatingModel={undefined}
          sizingMethod={SizingMethod.Count}
        />,
      )

      expect(screen.queryByText('Count-based Metrics')).not.toBeInTheDocument()
    })
  })
})
