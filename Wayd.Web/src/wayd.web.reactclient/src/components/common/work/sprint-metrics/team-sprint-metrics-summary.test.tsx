import { fireEvent, render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { SizingMethod, TeamSprintMetricsDto } from '@/src/services/wayd-api'
import { useGetTeamSprintMetricsQuery } from '@/src/store/features/organizations/team-api'
import TeamSprintMetricsSummary from './team-sprint-metrics-summary'

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamSprintMetricsQuery: jest.fn(),
}))

const summary = (average: number, min: number, max: number) => ({
  average,
  min,
  max,
  standardDeviation: 0,
  sprints: 2,
})

const sprint = (key: number, inEstimateAverages = true) => ({
  id: `sprint-${key}`,
  key,
  name: `Sprint ${key}`,
  start: '2026-06-01',
  end: '2026-06-14',
  sizingMethod: SizingMethod.StoryPoints,
  inEstimateAverages,
  byCount: { velocity: 4, predictability: 0.8, sayDo: 0.6 },
  byEstimate: { velocity: 21, predictability: 0.9, sayDo: 0.7 },
})

const createMetrics = (
  overrides: Partial<TeamSprintMetricsDto> = {},
): TeamSprintMetricsDto => ({
  teamId: 'team-1',
  sprintWindow: 7,
  defaultSprintWindow: 7,
  sizingMethod: SizingMethod.StoryPoints,
  byCount: {
    velocity: summary(4, 3, 5),
    predictability: summary(0.8, 0.7, 0.9),
    sayDo: summary(0.6, 0.5, 0.7),
  },
  byEstimate: {
    velocity: summary(24, 21, 27),
    predictability: summary(0.85, 0.8, 0.9),
    sayDo: summary(0.75, 0.7, 0.8),
  },
  sprints: [sprint(8), sprint(7)],
  excluded: [],
  ...overrides,
})

const mockQuery = (data: TeamSprintMetricsDto | undefined, isLoading = false) =>
  (useGetTeamSprintMetricsQuery as jest.Mock).mockReturnValue({
    data,
    isLoading,
  })

const card = (title: string) =>
  screen.getByText(title).closest('.ant-card') as HTMLElement

describe('TeamSprintMetricsSummary', () => {
  beforeEach(() => jest.clearAllMocks())

  it("shows the averages and ranges in the team's sizing method", () => {
    // Arrange
    mockQuery(createMetrics())

    // Act
    render(<TeamSprintMetricsSummary teamId="team-1" />)

    // Assert
    expect(card('Average Velocity')).toHaveTextContent('24')
    expect(card('Average Velocity')).toHaveTextContent('Range 21–27')
    expect(card('Average Predictability')).toHaveTextContent('85%')
    expect(card('Average Say/Do')).toHaveTextContent('Range 70%–80%')
    expect(
      screen.getByText('Last 2 completed standard sprints'),
    ).toBeInTheDocument()
  })

  it("asks for the organization's sprint window, not one of its own", () => {
    // Arrange
    mockQuery(createMetrics())

    // Act
    render(<TeamSprintMetricsSummary teamId="team-1" />)

    // Assert
    expect(useGetTeamSprintMetricsQuery).toHaveBeenCalledWith(
      { teamId: 'team-1' },
      { skip: false },
    )
  })

  it('counts work items for a team that sizes by count', () => {
    // Arrange
    mockQuery(createMetrics({ sizingMethod: SizingMethod.Count }))

    // Act
    render(<TeamSprintMetricsSummary teamId="team-1" />)

    // Assert
    expect(card('Average Velocity')).toHaveTextContent('Range 3–5')
  })

  it('says how many sprints the averages cover when the team has changed its sizing method', () => {
    // Arrange — one of the two sprints was measured in the old sizing method
    mockQuery(createMetrics({ sprints: [sprint(8), sprint(7, false)] }))

    // Act
    render(<TeamSprintMetricsSummary teamId="team-1" />)

    // Assert
    expect(
      screen.getByText(
        'Last 2 completed standard sprints: the 1 sprint sized in story points',
      ),
    ).toBeInTheDocument()
    expect(card('Average Velocity')).toHaveTextContent('24')
  })

  it("counts work items when no sprint is in the team's sizing method yet", () => {
    // Arrange
    mockQuery(
      createMetrics({
        sprints: [sprint(8, false), sprint(7, false)],
        byEstimate: {},
      }),
    )

    // Act
    render(<TeamSprintMetricsSummary teamId="team-1" />)

    // Assert
    expect(
      screen.getByText(
        'Last 2 completed standard sprints, by work item: none is sized in story points yet',
      ),
    ).toBeInTheDocument()
    expect(card('Average Velocity')).toHaveTextContent('Range 3–5')
  })

  it('opens the report from a card', () => {
    // Arrange
    mockQuery(createMetrics())
    const onOpenReport = jest.fn()
    render(
      <TeamSprintMetricsSummary teamId="team-1" onOpenReport={onOpenReport} />,
    )

    // Act
    fireEvent.click(card('Average Velocity'))

    // Assert
    expect(onOpenReport).toHaveBeenCalled()
  })

  it('renders nothing for a team with no sprint to measure', () => {
    // Arrange
    mockQuery(createMetrics({ sprints: [], byCount: {}, byEstimate: {} }))

    // Act
    const { container } = render(<TeamSprintMetricsSummary teamId="team-1" />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
