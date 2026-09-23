import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { TeamThroughputForecastDto } from '@/src/services/wayd-api'
import { ForecastOutcome } from './forecast-formatting'
import ThroughputForecast from './throughput-forecast'

jest.unmock('dayjs')
jest.mock('next/dynamic', () => ({
  __esModule: true,
  default: () => {
    const Component = () => <div data-testid="column-chart" />
    Component.displayName = 'MockColumnChart'
    return Component
  },
}))

jest.mock('../../contexts/theme', () => ({
  __esModule: true,
  default: jest.fn(() => ({
    antDesignChartsTheme: 'light',
    token: { colorPrimary: '#1677ff' },
  })),
}))

const createForecast = (
  overrides: Partial<TeamThroughputForecastDto> = {},
): TeamThroughputForecastDto =>
  ({
    outcome: { id: ForecastOutcome.Forecast, name: 'Forecast' },
    team: {
      team: { id: 't1', key: 7, name: 'Atlas', code: 'ATL', type: 'Team' },
      from: '2026-06-24',
      to: '2026-09-21',
      itemsCompleted: 45,
    },
    forecastStart: '2026-09-22',
    targetDate: '2026-10-05',
    days: 14,
    backlogWorkItems: 8,
    lookbackDays: 90,
    trials: 100,
    percentiles: [
      {
        confidence: 85,
        workItems: 6,
        throughWorkItem: {
          id: 'w6',
          key: 'CORE-6',
          workspaceKey: 'CORE',
          title: 'Sixth',
        },
      },
      { confidence: 50, workItems: 9, throughWorkItem: undefined },
    ],
    histogram: [{ workItems: 6, trials: 100 }],
    ...overrides,
  }) as unknown as TeamThroughputForecastDto

describe('ThroughputForecast', () => {
  it('shows the work items finished at each confidence and how far down the backlog', () => {
    render(<ThroughputForecast forecast={createForecast()} isLoading={false} />)

    expect(screen.getByText('85% likely')).toBeInTheDocument()
    expect(screen.getByText('6 work items')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'CORE-6' })).toHaveAttribute(
      'href',
      '/work/workspaces/CORE/work-items/CORE-6',
    )
    expect(screen.getByText('the whole backlog')).toBeInTheDocument()
  })

  it('explains too little history instead of forecasting', () => {
    render(
      <ThroughputForecast
        forecast={createForecast({
          outcome: {
            id: ForecastOutcome.NotEnoughHistory,
            name: 'Not Enough History',
          },
          percentiles: [],
          histogram: [],
        })}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Not Enough History')).toBeInTheDocument()
    expect(screen.queryByTestId('column-chart')).not.toBeInTheDocument()
  })
})
