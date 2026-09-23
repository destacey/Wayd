import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import { TeamThroughputForecastDto } from '@/src/services/wayd-api'
import ThroughputForecastChart from './throughput-forecast-chart'

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
    token: { colorPrimary: '#1677ff' },
  })),
}))

describe('ThroughputForecastChart', () => {
  it('orders counts and accumulates the share finishing at least each', () => {
    const forecast = {
      trials: 10,
      histogram: [
        { workItems: 6, trials: 2 },
        { workItems: 4, trials: 3 },
        { workItems: 5, trials: 5 },
      ],
    } as unknown as TeamThroughputForecastDto

    render(<ThroughputForecastChart forecast={forecast} />)

    const data = JSON.parse(
      screen.getByTestId('column-chart').getAttribute('data-config')!,
    ).data
    expect(data.map((d: any) => d.workItems)).toEqual(['4', '5', '6'])
    expect(data.map((d: any) => d.share)).toEqual([30, 50, 20])
    expect(data.map((d: any) => d.atLeast)).toEqual([1, 0.7, 0.2])
  })
})
