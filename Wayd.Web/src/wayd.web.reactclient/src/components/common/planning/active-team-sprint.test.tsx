import { render, screen } from '@testing-library/react'
import ActiveTeamSprint from './active-team-sprint'
import { SizingMethod } from '../../../services/wayd-api'

// Mock the API hooks
jest.mock('../../../store/features/organizations/team-api', () => ({
  useGetActiveSprintQuery: jest.fn(),
}))

jest.mock('../../../store/features/work-management/sprints-api', () => ({
  useGetSprintMetricsQuery: jest.fn(),
  useGetSprintScopeQuery: jest.fn(),
}))

jest.mock('./sprint-say-do-metric', () => ({
  __esModule: true,
  default: () => <div data-testid="say-do-metric" />,
}))

// Mock useTheme
jest.mock('../../contexts/theme', () => ({
  __esModule: true,
  default: () => ({
    token: {
      colorSuccess: '#52c41a',
      colorInfo: '#1677ff',
    },
  }),
}))

// Mock TimelineProgress
jest.mock('./timeline-progress', () => ({
  __esModule: true,
  default: () => <div data-testid="timeline-progress">Timeline Progress</div>,
}))

// Mock Metrics; the figures stay real so what is shown can be asserted.
jest.mock('../metrics', () => ({
  sprintOverviewFigures: jest.requireActual(
    '../metrics/sprint-overview-figures',
  ).sprintOverviewFigures,
  MetricCard: ({ title, value }: { title: string; value: number }) => (
    <div data-testid={`metric-${title}`}>{value}</div>
  ),
  CompletionRateMetric: ({
    completed,
    total,
    tooltip,
  }: {
    completed: number
    total: number
    tooltip?: string
  }) => (
    <div data-testid="completion-rate-metric">
      {completed}/{total} {tooltip}
    </div>
  ),
  StatusMetric: ({ value, tooltip }: { value: number; tooltip?: string }) => (
    <div data-testid="status-metric">
      <span data-testid="status-metric-value">{value}</span>
      <span>{tooltip}</span>
    </div>
  ),
  CycleTimeMetric: () => <div data-testid="cycle-time-metric" />,
}))

// Mock IterationHealthIndicator
jest.mock('./iteration-health-indicator', () => ({
  __esModule: true,
  default: ({ total, completed }: { total: number; completed: number }) => (
    <div data-testid="iteration-health-indicator">
      Health: {completed}/{total}
    </div>
  ),
}))

// Mock SprintPiPredictability
jest.mock('./sprint-pi-predictability', () => ({
  __esModule: true,
  default: () => null,
}))

import { useGetActiveSprintQuery } from '../../../store/features/organizations/team-api'
import {
  useGetSprintMetricsQuery,
  useGetSprintScopeQuery,
} from '../../../store/features/work-management/sprints-api'

describe('ActiveTeamSprint', () => {
  const mockSprint = {
    key: 'S1',
    name: 'Sprint 1',
    start: '2023-01-01',
    end: '2023-01-14',
    team: { id: 'team-1', key: 1, name: 'Team 1', code: 'T1' },
  }

  const mockMetrics = {
    sizingMethod: SizingMethod.Effort,
    totalEstimate: 40,
    completedEstimate: 25,
    inProgressEstimate: 9,
    notStartedEstimate: 6,
    totalWorkItems: 4,
    completedWorkItems: 2,
    inProgressWorkItems: 1,
    notStartedWorkItems: 1,
    unestimatedWorkItems: 0,
    cycleTime: {
      workItemsCount: 2,
      totalCycleTimeDays: 10,
      averageCycleTimeDays: 5,
    },
  }

  beforeEach(() => {
    ;(useGetActiveSprintQuery as jest.Mock).mockReturnValue({
      data: mockSprint,
      isLoading: false,
    })
    ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
      data: mockMetrics,
      isLoading: false,
    })
    ;(useGetSprintScopeQuery as jest.Mock).mockReturnValue({
      data: undefined,
      isLoading: false,
    })
  })

  it('renders active sprint details', () => {
    // Arrange / Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(screen.getByText('Active Sprint:')).toBeInTheDocument()
    expect(screen.getByText('Sprint 1')).toBeInTheDocument()
    expect(screen.getByTestId('timeline-progress')).toBeInTheDocument()
    expect(screen.getByTestId('completion-rate-metric')).toBeInTheDocument()
    expect(screen.getByTestId('metric-Velocity')).toBeInTheDocument()
    expect(screen.getByTestId('iteration-health-indicator')).toBeInTheDocument()
  })

  it('shows estimates in the sizing method the metrics report', () => {
    // Arrange / Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(screen.getByTestId('completion-rate-metric')).toHaveTextContent(
      '25/40 Effort',
    )
    expect(screen.getByTestId('metric-Velocity')).toHaveTextContent('25')
    expect(screen.getByTestId('status-metric-value')).toHaveTextContent('9')
    expect(screen.getByTestId('status-metric')).toHaveTextContent(
      'The effort in the sprint now',
    )
    expect(screen.getByTestId('iteration-health-indicator')).toHaveTextContent(
      'Health: 25/40',
    )
  })

  it('shows item counts for a Count-sized sprint', () => {
    // Arrange
    ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
      data: {
        ...mockMetrics,
        sizingMethod: SizingMethod.Count,
        totalEstimate: 4,
        completedEstimate: 2,
        inProgressEstimate: 1,
        notStartedEstimate: 1,
      },
      isLoading: false,
    })

    // Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(screen.getByTestId('completion-rate-metric')).toHaveTextContent(
      '2/4 Count',
    )
    expect(screen.getByTestId('status-metric')).toHaveTextContent(
      'The work items in the sprint now',
    )
  })

  it('shows predictability and say/do once the sprint has a commitment', () => {
    // Arrange
    const measure = (count: number, estimate: number) => ({ count, estimate })
    ;(useGetSprintScopeQuery as jest.Mock).mockReturnValue({
      data: {
        effectiveStart: '2023-01-02T00:00:00Z',
        historyIncomplete: false,
        totals: {
          total: measure(5, 48),
          committed: measure(4, 40),
          added: measure(1, 8),
          completed: measure(3, 30),
          removed: measure(0, 0),
          carriedOver: measure(0, 0),
          descoped: measure(1, 8),
          remaining: measure(1, 10),
          completedOfCommitted: measure(2, 22),
          sayDoCount: 0.5,
          sayDoEstimate: 0.55,
          unestimated: 0,
        },
      },
      isLoading: false,
    })

    // Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    // Velocity 30 of 40 committed.
    expect(screen.getByTestId('metric-Predictability')).toHaveTextContent('75')
    expect(
      screen.queryByTestId('completion-rate-metric'),
    ).not.toBeInTheDocument()
    expect(screen.getByTestId('iteration-health-indicator')).toHaveTextContent(
      'Health: 30/40',
    )
    expect(screen.getByTestId('say-do-metric')).toBeInTheDocument()
    expect(screen.queryByTestId('cycle-time-metric')).not.toBeInTheDocument()
  })

  it('fetches metrics for the active sprint', () => {
    // Arrange / Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(useGetActiveSprintQuery).toHaveBeenCalledWith('team-1')
    expect(useGetSprintMetricsQuery).toHaveBeenCalledWith('S1', {
      skip: false,
    })
  })

  it('renders skeleton when loading sprint', () => {
    // Arrange
    ;(useGetActiveSprintQuery as jest.Mock).mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    // Act
    const { container } = render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders loading state when loading metrics', () => {
    // Arrange
    ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    // Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(
      screen.queryByTestId('completion-rate-metric'),
    ).not.toBeInTheDocument()
  })

  it('renders nothing if no active sprint', () => {
    // Arrange
    ;(useGetActiveSprintQuery as jest.Mock).mockReturnValue({
      data: undefined,
      isLoading: false,
    })

    // Act
    const { container } = render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('displays link to sprint details page', () => {
    // Arrange / Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    const link = screen.getByRole('link', { name: 'Sprint 1' })
    expect(link).toHaveAttribute('href', '/work/sprints/S1')
  })

  it('links to the team instead of the "Active Sprint" label when showTeamLink is set', () => {
    // Arrange / Act
    render(<ActiveTeamSprint teamId="team-1" showTeamLink />)

    // Assert
    expect(screen.getByRole('link', { name: 'T1' })).toHaveAttribute(
      'href',
      '/organizations/teams/1',
    )
    expect(screen.queryByText('Active Sprint:')).not.toBeInTheDocument()
  })
})
