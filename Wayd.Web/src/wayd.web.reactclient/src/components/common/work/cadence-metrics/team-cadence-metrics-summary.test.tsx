import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { SizingMethod, TeamCadenceMetricsDto } from '@/src/services/wayd-api'
import { useGetTeamCadenceMetricsQuery } from '@/src/store/features/organizations/team-api'
import TeamCadenceMetricsSummary from './team-cadence-metrics-summary'

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamCadenceMetricsQuery: jest.fn(),
}))

const mockQuery = useGetTeamCadenceMetricsQuery as jest.Mock

const metrics = (
  overrides: Partial<TeamCadenceMetricsDto> = {},
): TeamCadenceMetricsDto => ({
  teamId: 'team-1',
  periodWindow: 7,
  defaultPeriodWindow: 7,
  cadenceWeeks: 2,
  sizingMethod: SizingMethod.StoryPoints,
  byCount: { average: 4, min: 3, max: 5, standardDeviation: 1, periods: 3 },
  byEstimate: {
    average: 17.6,
    min: 12,
    max: 22,
    standardDeviation: 4,
    periods: 3,
  },
  periods: [],
  ...overrides,
})

describe('TeamCadenceMetricsSummary', () => {
  it('shows the average velocity in the team sizing method', () => {
    // Arrange
    mockQuery.mockReturnValue({ data: metrics(), isLoading: false })

    // Act
    render(<TeamCadenceMetricsSummary teamId="team-1" />)

    // Assert
    expect(screen.getByText('18')).toBeInTheDocument()
    expect(screen.getByText('Last 3 periods')).toBeInTheDocument()
  })

  it('counts work items when no period is averaged in the team estimate yet', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: metrics({ byEstimate: undefined }),
      isLoading: false,
    })

    // Act
    render(<TeamCadenceMetricsSummary teamId="team-1" />)

    // Assert
    expect(screen.getByText('4')).toBeInTheDocument()
  })

  it('renders nothing with no period to average', () => {
    // Arrange
    mockQuery.mockReturnValue({
      data: metrics({ byCount: undefined, byEstimate: undefined }),
      isLoading: false,
    })

    // Act
    const { container } = render(<TeamCadenceMetricsSummary teamId="team-1" />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
