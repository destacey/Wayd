import { render, screen } from '@testing-library/react'
import HealthStatusHistoryChart, {
  convertHealthStatusToNumber,
  toHealthStatusHistorySeries,
} from './health-status-history-chart'

jest.mock('next/dynamic', () => ({
  __esModule: true,
  default: () => {
    const Component = (props: any) => (
      <div data-testid="line-chart" data-config={JSON.stringify(props)} />
    )
    Component.displayName = 'MockLineChart'
    return Component
  },
}))

jest.mock('../../contexts/theme', () => ({
  __esModule: true,
  default: jest.fn(() => ({
    currentTheme: 'wayd',
    currentMode: 'light',
    antDesignChartsTheme: 'light',
  })),
}))

describe('HealthStatusHistoryChart helpers', () => {
  it('should map health statuses to numbers', () => {
    expect(convertHealthStatusToNumber('Healthy')).toBe(2)
    expect(convertHealthStatusToNumber('At Risk')).toBe(1)
    expect(convertHealthStatusToNumber('Unhealthy')).toBe(0)
    expect(convertHealthStatusToNumber('Unknown')).toBe(0)
    expect(convertHealthStatusToNumber(undefined)).toBe(0)
  })

  it('should sort by report date and map status values', () => {
    const result = toHealthStatusHistorySeries([
      {
        reportedOn: new Date('2026-02-01T10:00:00.000Z'),
        status: { name: 'Unhealthy' },
      },
      {
        reportedOn: new Date('2026-01-15T10:00:00.000Z'),
        status: { name: 'Healthy' },
      },
      {
        reportedOn: new Date('2026-01-20T10:00:00.000Z'),
        status: { name: 'At Risk' },
      },
    ])

    expect(result).toHaveLength(3)
    expect(result[0].status).toBe(2)
    expect(result[1].status).toBe(1)
    expect(result[2].status).toBe(0)
    expect(result[0].date.toISOString()).toBe('2026-01-15T10:00:00.000Z')
    expect(result[1].date.toISOString()).toBe('2026-01-20T10:00:00.000Z')
    expect(result[2].date.toISOString()).toBe('2026-02-01T10:00:00.000Z')
  })
})

describe('HealthStatusHistoryChart', () => {
  it('should render a line chart with transformed data', () => {
    render(
      <HealthStatusHistoryChart
        data={[
          {
            reportedOn: new Date('2026-02-01T10:00:00.000Z'),
            status: { name: 'Unhealthy' },
          },
          {
            reportedOn: new Date('2026-01-15T10:00:00.000Z'),
            status: { name: 'Healthy' },
          },
        ]}
      />,
    )

    const chart = screen.getByTestId('line-chart')
    const config = JSON.parse(chart.getAttribute('data-config') || '{}')

    expect(config.theme).toBe('light')
    expect(config.height).toBe(180)
    expect(config.autoFit).toBe(true)
    expect(config.padding).toEqual([8, 8, 24, 56])
    expect(config.xField).toBe('date')
    expect(config.yField).toBe('status')
    expect(config.data).toHaveLength(2)
    expect(config.data[0].status).toBe(2)
    expect(config.data[1].status).toBe(0)
  })

  it('should show card loading state', () => {
    const { container } = render(<HealthStatusHistoryChart isLoading />)
    // ChartCard draws a chart-shaped skeleton in the body rather than antd's
    // paragraph placeholder, so the title survives the load.
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('should show empty state when data is an empty array', () => {
    render(<HealthStatusHistoryChart data={[]} />)

    expect(screen.getByText('No health checks found.')).toBeInTheDocument()
    expect(screen.queryByTestId('line-chart')).not.toBeInTheDocument()
  })

  it('should show empty state when data is undefined', () => {
    render(<HealthStatusHistoryChart data={undefined} />)

    expect(screen.getByText('No health checks found.')).toBeInTheDocument()
    expect(screen.queryByTestId('line-chart')).not.toBeInTheDocument()
  })
})
