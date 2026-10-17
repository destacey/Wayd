import { render, screen, within } from '@testing-library/react'
import '@testing-library/jest-dom'
import {
  SizingMethod,
  TeamCadenceMetricsDto,
  TeamCadencePeriodDto,
} from '@/src/services/wayd-api'
import {
  TeamCadenceMetricsReportView,
  TeamCadenceMetricsReportViewProps,
} from './team-cadence-metrics-report'

jest.unmock('dayjs')

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamCadenceMetricsQuery: jest.fn(),
}))

// The chart needs a canvas; the table carries the same figures.
jest.mock('./team-cadence-velocity-chart', () => ({
  __esModule: true,
  default: () => <div data-testid="velocity-chart" />,
}))

const period = (
  start: string,
  end: string,
  overrides: Partial<TeamCadencePeriodDto> = {},
): TeamCadencePeriodDto => ({
  start,
  end,
  sizingMethod: SizingMethod.StoryPoints,
  isPartial: false,
  isCurrent: false,
  inAverages: true,
  inEstimateAverages: true,
  completedCount: 4,
  completedEstimate: 21,
  unestimatedCount: 1,
  ...overrides,
})

const createMetrics = (
  overrides: Partial<TeamCadenceMetricsDto> = {},
): TeamCadenceMetricsDto => ({
  teamId: 'team-1',
  periodWindow: 7,
  defaultPeriodWindow: 7,
  cadenceWeeks: 2,
  sizingMethod: SizingMethod.StoryPoints,
  byCount: { average: 4, min: 4, max: 4, standardDeviation: 0, periods: 1 },
  byEstimate: {
    average: 24.4,
    min: 21,
    max: 28,
    standardDeviation: 3.5,
    periods: 2,
  },
  periods: [
    period('2026-10-12', '2026-10-25', {
      isCurrent: true,
      inAverages: false,
      inEstimateAverages: false,
      completedCount: 1,
      completedEstimate: 3,
    }),
    period('2026-09-28', '2026-10-11'),
  ],
  ...overrides,
})

const renderView = (props: Partial<TeamCadenceMetricsReportViewProps> = {}) =>
  render(
    <TeamCadenceMetricsReportView
      metrics={createMetrics()}
      isLoading={false}
      onPeriodWindowChange={jest.fn()}
      byCount={false}
      onByCountChange={jest.fn()}
      {...props}
    />,
  )

describe('TeamCadenceMetricsReportView', () => {
  it('shows the average velocity in the team sizing method', () => {
    // Arrange & Act
    renderView()

    // Assert
    expect(screen.getByText('Average Velocity')).toBeInTheDocument()
    expect(screen.getByText('24')).toBeInTheDocument()
    expect(screen.getByText('Range 21–28')).toBeInTheDocument()
    expect(screen.getByText('Every 2 weeks')).toBeInTheDocument()
  })

  it('marks the current period as in progress', () => {
    // Arrange & Act
    renderView()

    // Assert
    const rows = screen.getAllByRole('row')
    expect(within(rows[1]).getByText('In progress')).toBeInTheDocument()
    expect(within(rows[2]).queryByText('In progress')).not.toBeInTheDocument()
  })

  it('counts work items when shown by count', () => {
    // Arrange & Act
    renderView({ byCount: true })

    // Assert
    const rows = screen.getAllByRole('row')
    expect(within(rows[2]).getByText('4')).toBeInTheDocument()
    expect(screen.queryByText('Unestimated')).not.toBeInTheDocument()
  })

  it('hides an estimate measured in another sizing method', () => {
    // Arrange
    const metrics = createMetrics({
      periods: [
        period('2026-09-28', '2026-10-11', {
          sizingMethod: SizingMethod.Effort,
          inEstimateAverages: false,
        }),
      ],
    })

    // Act
    renderView({ metrics })

    // Assert
    expect(
      screen.getByText('1 period was measured in another sizing method'),
    ).toBeInTheDocument()
  })

  it('explains a team with no cadence', () => {
    // Arrange & Act
    renderView({
      metrics: createMetrics({
        cadenceWeeks: undefined,
        byCount: undefined,
        byEstimate: undefined,
        periods: [],
      }),
    })

    // Assert
    expect(screen.getByText(/The team has no cadence/)).toBeInTheDocument()
  })
})
