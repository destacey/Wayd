import { render, screen } from '@testing-library/react'
import ActiveTeamSprint from './active-team-sprint'
import { SizingMethod } from '../../../services/wayd-api'

// Mock the API hooks
jest.mock('../../../store/features/organizations/team-api', () => ({
  useGetActiveSprintQuery: jest.fn(),
  useGetTeamDetailsQuery: jest.fn(() => ({ data: undefined })),
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

// Mock Metrics
jest.mock('../metrics', () => ({
  CompletionRateMetric: () => <div data-testid="completion-rate-metric" />,
  VelocityMetric: () => <div data-testid="velocity-metric" />,
  StatusMetric: () => <div data-testid="status-metric" />,
  CycleTimeMetric: () => <div data-testid="cycle-time-metric" />,
}))

// Mock IterationHealthIndicator
jest.mock('./iteration-health-indicator', () => ({
  __esModule: true,
  default: () => (
    <div data-testid="iteration-health-indicator">Health Indicator</div>
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
    totalStoryPoints: 10,
    completedStoryPoints: 5,
    inProgressStoryPoints: 3,
    notStartedStoryPoints: 2,
    totalWorkItems: 4,
    completedWorkItems: 2,
    inProgressWorkItems: 1,
    notStartedWorkItems: 1,
    cycleTime: { workItemsCount: 2, totalCycleTimeDays: 10, averageCycleTimeDays: 5 },
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

  it('renders active sprint details with story points sizing', () => {
    render(
      <ActiveTeamSprint
        teamId="team-1"
        sizingMethod={SizingMethod.StoryPoints}
      />,
    )

    expect(screen.getByText('Active Sprint:')).toBeInTheDocument()
    expect(screen.getByText('Sprint 1')).toBeInTheDocument()
    expect(screen.getByTestId('timeline-progress')).toBeInTheDocument()
    expect(screen.getByTestId('completion-rate-metric')).toBeInTheDocument()
    expect(screen.getByTestId('velocity-metric')).toBeInTheDocument()
    expect(screen.getByTestId('iteration-health-indicator')).toBeInTheDocument()
  })

  it('renders active sprint details with count sizing', () => {
    render(
      <ActiveTeamSprint teamId="team-1" sizingMethod={SizingMethod.Count} />,
    )

    expect(screen.getByText('Active Sprint:')).toBeInTheDocument()
    expect(screen.getByText('Sprint 1')).toBeInTheDocument()
    expect(screen.getByTestId('timeline-progress')).toBeInTheDocument()
    expect(screen.getByTestId('completion-rate-metric')).toBeInTheDocument()
    expect(screen.getByTestId('velocity-metric')).toBeInTheDocument()
    expect(screen.getByTestId('iteration-health-indicator')).toBeInTheDocument()
  })

  it('renders skeleton when loading sprint', () => {
    ;(useGetActiveSprintQuery as jest.Mock).mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    const { container } = render(
      <ActiveTeamSprint
        teamId="team-1"
        sizingMethod={SizingMethod.StoryPoints}
      />,
    )
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders loading state when loading metrics', () => {
    ;(useGetSprintMetricsQuery as jest.Mock).mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    render(
      <ActiveTeamSprint
        teamId="team-1"
        sizingMethod={SizingMethod.StoryPoints}
      />,
    )
    expect(
      screen.queryByTestId('completion-rate-metric'),
    ).not.toBeInTheDocument()
  })

  it('renders nothing if no active sprint', () => {
    ;(useGetActiveSprintQuery as jest.Mock).mockReturnValue({
      data: undefined,
      isLoading: false,
    })

    const { container } = render(
      <ActiveTeamSprint
        teamId="team-1"
        sizingMethod={SizingMethod.StoryPoints}
      />,
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('renders health indicator with correct data', () => {
    render(
      <ActiveTeamSprint
        teamId="team-1"
        sizingMethod={SizingMethod.StoryPoints}
      />,
    )

    expect(screen.getByTestId('iteration-health-indicator')).toBeInTheDocument()
  })

  it('displays link to sprint details page', () => {
    render(
      <ActiveTeamSprint
        teamId="team-1"
        sizingMethod={SizingMethod.StoryPoints}
      />,
    )

    const link = screen.getByRole('link', { name: 'Sprint 1' })
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/work/sprints/S1')
  })
})
