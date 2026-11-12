'use client'

import type { ColumnConfig } from '@ant-design/charts'
import { TeamThroughputForecastDto } from '@/src/services/wayd-api'
import { FC } from 'react'
import useTheme from '../../contexts/theme'
import { formatPercent } from './forecast-formatting'
import { Column } from '@/src/components/common/charts'

export interface ThroughputForecastChartProps {
  forecast: TeamThroughputForecastDto
}

interface ChartDatum {
  workItems: string
  share: number
  atLeast: number
}

/**
 * The share of simulations finishing each number of work items by the target
 * date.
 */
const ThroughputForecastChart: FC<ThroughputForecastChartProps> = ({
  forecast,
}) => {
  const { antDesignChartsTheme, token } = useTheme()

  // "At least n" accumulates from the high end: every simulation that
  // finished n or more.
  const data = [...forecast.histogram]
    .sort((a, b) => b.workItems - a.workItems)
    .reduce<ChartDatum[]>((points, bucket) => {
      const share = bucket.trials / forecast.trials
      return [
        {
          workItems: bucket.workItems.toString(),
          share: share * 100,
          atLeast: (points[0]?.atLeast ?? 0) + share,
        },
        ...points,
      ]
    }, [])

  const config = {
    theme: antDesignChartsTheme,
    data,
    height: 280,
    xField: 'workItems',
    yField: 'share',
    axis: {
      x: { title: 'Work items finished', labelAutoHide: true },
      y: {
        title: 'Simulations (%)',
        labelFormatter: (value: number) => `${value}%`,
      },
    },
    tooltip: {
      title: (datum: ChartDatum) => `${datum.workItems} work items`,
      items: [
        {
          name: 'Finish exactly this many',
          field: 'share',
          valueFormatter: (value: number) => `${value.toFixed(1)}%`,
        },
        {
          name: 'Finish at least this many',
          field: 'atLeast',
          valueFormatter: (value: number) => formatPercent(value),
        },
      ],
    },
    style: {
      radiusTopLeft: 4,
      radiusTopRight: 4,
      fill: token.colorPrimary,
    },
  } as ColumnConfig

  return <Column {...(config as any)} />
}

export default ThroughputForecastChart
