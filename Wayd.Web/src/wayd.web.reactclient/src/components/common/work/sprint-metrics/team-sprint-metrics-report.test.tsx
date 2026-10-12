import { render, screen, within } from '@testing-library/react'
import '@testing-library/jest-dom'
import {
  SizingMethod,
  SprintMetricsExclusion,
  TeamSprintMetricsDto,
  TeamSprintMetricsSprintDto,
} from '@/src/services/wayd-api'
import {
  TeamSprintMetricsReportView,
  TeamSprintMetricsReportViewProps,
} from './team-sprint-metrics-report'

jest.unmock('dayjs')

jest.mock('next/link', () => {
  const MockLink = ({ href, children }: any) => <a href={href}>{children}</a>
  MockLink.displayName = 'MockLink'
  return MockLink
})

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamSprintMetricsQuery: jest.fn(),
}))

const sprint = (
  key: number,
  overrides: Partial<TeamSprintMetricsSprintDto> = {},
): TeamSprintMetricsSprintDto => ({
  id: `sprint-${key}`,
  key,
  name: `Sprint ${key}`,
  start: '2026-06-01',
  end: '2026-06-14',
  sizingMethod: SizingMethod.StoryPoints,
  inEstimateAverages: true,
  byCount: { velocity: 4, predictability: 0.8, sayDo: 0.6 },
  byEstimate: { velocity: 21, predictability: 0.9, sayDo: 0.7 },
  ...overrides,
})

const createMetrics = (
  overrides: Partial<TeamSprintMetricsDto> = {},
): TeamSprintMetricsDto => ({
  teamId: 'team-1',
  sprintWindow: 7,
  defaultSprintWindow: 7,
  sizingMethod: SizingMethod.StoryPoints,
  byCount: {
    velocity: {
      average: 4.5,
      min: 3,
      max: 6,
      standardDeviation: 1,
      sprints: 2,
    },
    predictability: {
      average: 0.8,
      min: 0.7,
      max: 0.9,
      standardDeviation: 0.1,
      sprints: 2,
    },
    sayDo: {
      average: 0.6,
      min: 0.5,
      max: 0.7,
      standardDeviation: 0.1,
      sprints: 2,
    },
  },
  byEstimate: {
    velocity: {
      average: 24.5,
      min: 21,
      max: 28,
      standardDeviation: 3.5,
      sprints: 2,
    },
    predictability: {
      average: 0.85,
      min: 0.8,
      max: 0.9,
      standardDeviation: 0.05,
      sprints: 2,
    },
    sayDo: {
      average: 0.75,
      min: 0.7,
      max: 0.8,
      standardDeviation: 0.05,
      sprints: 2,
    },
  },
  sprints: [sprint(8), sprint(7)],
  excluded: [],
  ...overrides,
})

const renderView = (
  overrides: Partial<TeamSprintMetricsReportViewProps> = {},
) =>
  render(
    <TeamSprintMetricsReportView
      metrics={createMetrics()}
      isLoading={false}
      onSprintWindowChange={jest.fn()}
      byCount={false}
      onByCountChange={jest.fn()}
      {...overrides}
    />,
  )

const card = (title: string) =>
  screen.getByText(title).closest('.ant-card') as HTMLElement

describe('TeamSprintMetricsReportView', () => {
  it("shows each metric's average and range in the team's sizing method", () => {
    // Arrange / Act
    renderView()

    // Assert
    expect(card('Average Velocity')).toHaveTextContent('24.5')
    expect(
      within(card('Average Velocity')).getByText('Range 21–28'),
    ).toBeInTheDocument()
    expect(
      within(card('Average Predictability')).getByText('85%'),
    ).toBeInTheDocument()
    expect(
      within(card('Average Say/Do')).getByText('Range 70%–80%'),
    ).toBeInTheDocument()
  })

  it('shows the averages by item count when switched to Count', () => {
    // Arrange / Act
    renderView({ byCount: true })

    // Assert
    expect(card('Average Velocity')).toHaveTextContent('4.5')
    expect(
      within(card('Average Velocity')).getByText('Range 3–6'),
    ).toBeInTheDocument()
  })

  it("lists each sprint's values beside the averages", () => {
    // Arrange / Act
    renderView()

    // Assert
    const row = screen.getByRole('link', { name: 'Sprint 8' }).closest('tr')!
    expect(screen.getByRole('link', { name: 'Sprint 8' })).toHaveAttribute(
      'href',
      '/work/sprints/8',
    )
    expect(within(row).getByText('21')).toBeInTheDocument()
    expect(within(row).getByText('90%')).toBeInTheDocument()
    expect(within(row).getByText('70%')).toBeInTheDocument()
  })

  it('lists the sprints left out with the reason', () => {
    // Arrange / Act
    renderView({
      metrics: createMetrics({
        excluded: [
          {
            id: 'sprint-9',
            key: 9,
            name: 'IP Sprint',
            start: '2026-06-15',
            end: '2026-06-28',
            reason: SprintMetricsExclusion.NonStandard,
          },
        ],
      }),
    })

    // Assert
    const row = screen.getByRole('link', { name: 'IP Sprint' }).closest('tr')!
    expect(within(row).getByText('Non-standard sprint')).toBeInTheDocument()
  })

  it('leaves a sprint in another sizing method out of the estimate values and says so', () => {
    // Arrange / Act
    renderView({
      metrics: createMetrics({
        sprints: [
          sprint(8),
          sprint(7, {
            sizingMethod: SizingMethod.Effort,
            inEstimateAverages: false,
            byEstimate: { velocity: 300, predictability: 1, sayDo: 1 },
          }),
        ],
      }),
    })

    // Assert
    expect(
      screen.getByText('1 sprint was measured in another sizing method'),
    ).toBeInTheDocument()
    const row = screen.getByRole('link', { name: 'Sprint 7' }).closest('tr')!
    expect(within(row).queryByText('300')).not.toBeInTheDocument()
    expect(within(row).getAllByText('—')).toHaveLength(3)
  })

  it('shows that sprint by count, where every sprint compares', () => {
    // Arrange / Act
    renderView({
      byCount: true,
      metrics: createMetrics({
        sprints: [
          sprint(7, {
            sizingMethod: SizingMethod.Effort,
            inEstimateAverages: false,
          }),
        ],
      }),
    })

    // Assert
    expect(
      screen.queryByText('1 sprint was measured in another sizing method'),
    ).not.toBeInTheDocument()
    const row = screen.getByRole('link', { name: 'Sprint 7' }).closest('tr')!
    expect(within(row).getByText('4')).toBeInTheDocument()
  })

  it('counts work items for a team that sizes by count, with no unit to switch', () => {
    // Arrange / Act
    renderView({
      metrics: createMetrics({ sizingMethod: SizingMethod.Count }),
    })

    // Assert
    expect(card('Average Velocity')).toHaveTextContent('4.5')
    expect(screen.queryByText('Story Points')).not.toBeInTheDocument()
  })

  it('says how many sprints it found when the window could not be filled', () => {
    // Arrange / Act
    renderView({ metrics: createMetrics({ sprintWindow: 7 }) })

    // Assert
    expect(
      screen.getByText(
        'Measured over 2 sprints: the team has fewer than 7 usable among its most recent completed sprints.',
      ),
    ).toBeInTheDocument()
  })

  it('marks the organization default among the windows and shows the one in use', () => {
    // Arrange / Act
    renderView()

    // Assert
    expect(screen.getByText('7 sprints (default)')).toBeInTheDocument()
  })

  it('says so when there is nothing to measure, and still lists what was left out', () => {
    // Arrange / Act
    renderView({
      metrics: createMetrics({
        sprints: [],
        byCount: {},
        byEstimate: {},
        sizingMethod: SizingMethod.Count,
        excluded: [
          {
            id: 'sprint-1',
            key: 1,
            name: 'Sprint 1',
            start: '2026-06-01',
            end: '2026-06-14',
            reason: SprintMetricsExclusion.HistoryIncomplete,
          },
        ],
      }),
    })

    // Assert
    expect(
      screen.getByText(
        'No completed standard sprints with a commitment to measure yet',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('History incomplete')).toBeInTheDocument()
  })

  it('reports a failed load', () => {
    // Arrange / Act
    renderView({ metrics: undefined, error: { status: 500 } })

    // Assert
    expect(
      screen.getByText("The team's sprint metrics could not be loaded."),
    ).toBeInTheDocument()
  })
})
