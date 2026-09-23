import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { WorkItemForecastDto } from '@/src/services/wayd-api'
import CompletionForecast from './completion-forecast'
import { ForecastOutcome } from './forecast-formatting'

jest.unmock('dayjs')
jest.mock('next/dynamic', () => ({
  __esModule: true,
  default: () => {
    const Component = (props: any) => (
      <div data-testid="column-chart" data-config={JSON.stringify(props)} />
    )
    Component.displayName = 'MockColumnChart'
    return Component
  },
}))

jest.mock('../../contexts/theme', () => ({
  __esModule: true,
  default: jest.fn(() => ({
    antDesignChartsTheme: 'light',
    token: { colorPrimary: '#1677ff', colorPrimaryBorder: '#91caff' },
  })),
}))

const workItem = (key: string) => ({
  id: `${key}-id`,
  key,
  workspaceKey: key.split('-')[0],
  title: `${key} title`,
})

const createForecast = (
  overrides: Partial<WorkItemForecastDto> = {},
): WorkItemForecastDto =>
  ({
    outcome: { id: ForecastOutcome.Forecast, name: 'Forecast' },
    forecastStart: '2026-09-22',
    remainingWorkItems: 3,
    excludedWorkItems: [],
    teams: [
      {
        team: { id: 't1', key: 7, name: 'Atlas', code: 'ATL', type: 'Team' },
        from: '2026-06-24',
        to: '2026-09-21',
        itemsCompleted: 45,
      },
    ],
    lookbackDays: 90,
    ignoreDependencies: false,
    trials: 100,
    trialsBeyondHorizon: 0,
    percentiles: [
      { confidence: 50, date: '2026-10-01' },
      { confidence: 70, date: '2026-10-05' },
      { confidence: 85, date: '2026-10-09' },
      { confidence: 95, date: undefined },
    ],
    histogram: [
      { date: '2026-10-01', trials: 60 },
      { date: '2026-10-09', trials: 40 },
    ],
    dependencies: [],
    ignoredDependencies: [],
    issues: [],
    ...overrides,
  }) as unknown as WorkItemForecastDto

describe('CompletionForecast', () => {
  it('shows the date at each confidence level', () => {
    render(<CompletionForecast forecast={createForecast()} isLoading={false} />)

    expect(screen.getByText('85% likely by')).toBeInTheDocument()
    expect(screen.getByText('Oct 9, 2026')).toBeInTheDocument()
    expect(screen.getByText('Beyond 2 years')).toBeInTheDocument()
  })

  it('shows the chance of finishing by the target date', () => {
    render(
      <CompletionForecast
        forecast={createForecast({
          targetDate: '2026-10-05' as unknown as Date,
          chanceOfFinishingByTargetDate: 0.62,
        })}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Chance by Oct 5, 2026')).toBeInTheDocument()
    expect(screen.getByText('62%')).toBeInTheDocument()
  })

  it('names the teams and history the forecast is based on', () => {
    render(<CompletionForecast forecast={createForecast()} isLoading={false} />)

    expect(screen.getByRole('link', { name: 'Atlas' })).toHaveAttribute(
      'href',
      '/organizations/teams/7',
    )
    expect(
      screen.getByText(/finished 45 backlog work items/),
    ).toBeInTheDocument()
  })

  it('explains an outcome without a forecast and hides the dates', () => {
    render(
      <CompletionForecast
        forecast={createForecast({
          outcome: {
            id: ForecastOutcome.BlockedByDependency,
            name: 'Blocked by Dependency',
          },
          percentiles: [],
          histogram: [],
          issues: [
            {
              workItem: workItem('CORE-12'),
              type: { id: 2, name: 'No Team' },
            },
          ],
        })}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Blocked by Dependency')).toBeInTheDocument()
    expect(screen.queryByText(/likely by/)).not.toBeInTheDocument()
    expect(screen.getByText('No Team')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'CORE-12' })).toHaveAttribute(
      'href',
      '/work/workspaces/CORE/work-items/CORE-12',
    )
  })

  it('lists excluded work items as making the forecast a lower bound', () => {
    render(
      <CompletionForecast
        forecast={createForecast({ excludedWorkItems: [workItem('CORE-3')] })}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Left out of the forecast')).toBeInTheDocument()
    expect(screen.getByText(/lower bound/)).toBeInTheDocument()
  })

  it('orders dependencies by how often they set the finish', () => {
    render(
      <CompletionForecast
        forecast={createForecast({
          dependencies: [
            {
              predecessor: workItem('CORE-1'),
              successor: workItem('CORE-9'),
              shareOfTrialsSettingFinish: 0.1,
            },
            {
              predecessor: workItem('PLAT-2'),
              successor: workItem('CORE-9'),
              shareOfTrialsSettingFinish: 0.64,
            },
          ],
        })}
        isLoading={false}
      />,
    )

    const predecessors = screen
      .getAllByRole('link')
      .map((link) => link.textContent)
      .filter((text) => text === 'CORE-1' || text === 'PLAT-2')
    expect(predecessors).toEqual(['PLAT-2', 'CORE-1'])
  })

  it('states the history window it used', () => {
    render(
      <CompletionForecast
        forecast={createForecast({ lookbackDays: 60 })}
        isLoading={false}
      />,
    )

    expect(
      screen.getByText(/from the last 60 days of history/),
    ).toBeInTheDocument()
  })

  it('marks a forecast that ignored dependencies as a what-if', () => {
    render(
      <CompletionForecast
        forecast={createForecast({ ignoreDependencies: true })}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Dependencies ignored')).toBeInTheDocument()
  })

  it('names why each ignored dependency was left out', () => {
    render(
      <CompletionForecast
        forecast={createForecast({
          ignoredDependencies: [
            {
              predecessor: workItem('AHTG-118322'),
              successor: workItem('CORE-9'),
              reason: { id: 2, name: 'Predecessor Removed' },
            },
          ],
        })}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Ignored dependencies')).toBeInTheDocument()
    expect(screen.getByText('Predecessor Removed')).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'AHTG-118322' }),
    ).toBeInTheDocument()
  })

  it('shows an error when the forecast fails to load', () => {
    render(<CompletionForecast isLoading={false} error={new Error('boom')} />)

    expect(screen.getByText('Unable to load the forecast.')).toBeInTheDocument()
  })
})
