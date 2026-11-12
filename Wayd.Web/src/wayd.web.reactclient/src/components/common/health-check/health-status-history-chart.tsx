'use client'

import { ChartCard } from '../metrics'
import dayjs from 'dayjs'
import useTheme from '../../contexts/theme'
import WaydEmpty from '../wayd-empty'
import { Line } from '@/src/components/common/charts'

export interface HealthStatusHistoryItem {
  reportedOn: Date
  status?: { name: string } | null
}

export interface HealthStatusHistoryChartPoint {
  date: Date
  status: number
}

export interface HealthStatusHistoryChartProps {
  data?: HealthStatusHistoryItem[]
  isLoading?: boolean
  title?: string
  height?: number
  cardStyle?: React.CSSProperties
}

export const convertHealthStatusToNumber = (status?: string | null) => {
  switch (status) {
    case 'Healthy':
      return 2
    case 'At Risk':
      return 1
    case 'Unhealthy':
      return 0
    default:
      return 0
  }
}

export const healthStatusMap: Record<number, string> = {
  0: 'Unhealthy',
  1: 'At Risk',
  2: 'Healthy',
}

export const toHealthStatusHistorySeries = (
  healthReportData?: HealthStatusHistoryItem[],
): HealthStatusHistoryChartPoint[] => {
  if (!healthReportData) return []

  return healthReportData
    .slice()
    .sort(
      (a, b) => dayjs(a.reportedOn).valueOf() - dayjs(b.reportedOn).valueOf(),
    )
    .map((report) => ({
      date: new Date(report.reportedOn),
      status: convertHealthStatusToNumber(report.status?.name),
    }))
}

const HealthStatusHistoryChart = ({
  data,
  isLoading = false,
  title = 'Health Status History',
  height = 180,
  cardStyle,
}: HealthStatusHistoryChartProps) => {
  const { antDesignChartsTheme } = useTheme()

  const seriesData = toHealthStatusHistorySeries(data)

  const spansMultipleDays =
    seriesData.length > 1 &&
    !dayjs(seriesData[0].date).isSame(
      dayjs(seriesData[seriesData.length - 1].date),
      'day',
    )

  const xLabelFormatter = (value: Date) =>
    spansMultipleDays
      ? dayjs(value).format('MMM D')
      : dayjs(value).format('h:mm A')

  const config = {
    theme: antDesignChartsTheme,
    autoFit: true,
    height,
    padding: [8, 8, 24, 56],
    data: seriesData,
    xField: 'date',
    yField: 'status',
    point: {
      size: 4,
      shape: 'circle',
    },
    tooltip: {
      title: (datum: any) => dayjs(datum.date).format('MMM D, YYYY'),
      items: [
        {
          channel: 'y',
          valueFormatter: (value: any) => healthStatusMap[value],
          name: 'Status',
        },
        {
          channel: 'x',
          valueFormatter: (value: any) => dayjs(value).format('h:mm A'),
          name: 'Time',
        },
      ],
    },
    axis: {
      x: {
        gridStrokeOpacity: 0.3,
        labelFormatter: xLabelFormatter,
        tickCount: seriesData.length,
      },
      y: {
        labelFormatter: (value: any) => healthStatusMap[value],
        gridStrokeOpacity: 0.3,
      },
    },
    scale: {
      y: {
        type: 'linear',
        domain: [0, 1, 2],
        tickMethod: () => [0, 1, 2],
      },
      x: {
        type: 'time',
        tickCount: seriesData.length,
      },
    },
  } as any

  return (
    <ChartCard
      title={title}
      loading={isLoading}
      cardStyle={{ minHeight: 200, width: '100%', minWidth: 280, ...cardStyle }}
    >
      {seriesData.length === 0 ? (
        <WaydEmpty message="No health checks found." />
      ) : (
        <Line {...config} />
      )}
    </ChartCard>
  )
}

export default HealthStatusHistoryChart
