import { render, screen } from '@testing-library/react'
import ActiveTeamSprint from './active-team-sprint'
import { SizingMethod } from '../../../services/wayd-api'

// Mock the API hooks
jest.mock('../../../store/features/organizations/team-api', () => ({
  useGetActiveSprintQuery: jest.fn(),
}))

jest.mock('../../../store/features/work-management/sprints-api', () => ({
  useGetSprintMetricsQuery: jest.fn(),
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

// Mock Metrics; sprintMetricValues stays real so the figures shown can be asserted.
jest.mock('../metrics', () => ({
  sprintMetricValues: jest.requireActual('../metrics/sprint-metric-values')
    .sprintMetricValues,
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
  VelocityMetric: ({
    completed,
    tooltip,
  }: {
    completed: number
    tooltip?: string
  }) => (
    <div data-testid="velocity-metric">
      {completed} {tooltip}
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
import { useGetSprintMetricsQuery } from '../../../store/features/work-management/sprints-api'

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
  })

  it('renders active sprint details', () => {
    // Arrange / Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(screen.getByText('Active Sprint:')).toBeInTheDocument()
    expect(screen.getByText('Sprint 1')).toBeInTheDocument()
    expect(screen.getByTestId('timeline-progress')).toBeInTheDocument()
    expect(screen.getByTestId('completion-rate-metric')).toBeInTheDocument()
    expect(screen.getByTestId('velocity-metric')).toBeInTheDocument()
    expect(screen.getByTestId('iteration-health-indicator')).toBeInTheDocument()
  })

  it('shows estimates in the sizing method the metrics report', () => {
    // Arrange / Act
    render(<ActiveTeamSprint teamId="team-1" />)

    // Assert
    expect(screen.getByTestId('completion-rate-metric')).toHaveTextContent(
      '25/40 Effort',
    )
    expect(screen.getByTestId('velocity-metric')).toHaveTextContent('25 Effort')
    expect(screen.getByTestId('status-metric-value')).toHaveTextContent('9')
    expect(screen.getByTestId('status-metric')).toHaveTextContent(
      'Total effort currently in the sprint',
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
      'Total work items currently in the sprint',
    )
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
