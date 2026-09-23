import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { WorkItemForecastDto } from '@/src/services/wayd-api'
import CompletionForecastChart from './completion-forecast-chart'

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

const chartData = () =>
  JSON.parse(screen.getByTestId('column-chart').getAttribute('data-config')!)
    .data

describe('CompletionForecastChart', () => {
  it('orders dates and accumulates the share finished by each', () => {
    const forecast = {
      trials: 10,
      histogram: [
        { date: '2026-10-03', trials: 5 },
        { date: '2026-10-01', trials: 2 },
        { date: '2026-10-02', trials: 3 },
      ],
    } as unknown as WorkItemForecastDto

    render(<CompletionForecastChart forecast={forecast} />)

    const data = chartData()
    expect(data.map((d: any) => d.label)).toEqual(['Oct 1', 'Oct 2', 'Oct 3'])
    expect(data.map((d: any) => d.share)).toEqual([20, 30, 50])
    expect(data.map((d: any) => d.cumulative)).toEqual([0.2, 0.5, 1])
  })

  it('marks dates after the target date', () => {
    const forecast = {
      trials: 2,
      histogram: [
        { date: '2026-10-01', trials: 1 },
        { date: '2026-10-02', trials: 1 },
      ],
    } as unknown as WorkItemForecastDto

    render(
      <CompletionForecastChart forecast={forecast} targetDate="2026-10-01" />,
    )

    expect(chartData().map((d: any) => d.byTargetDate)).toEqual([true, false])
  })
})
