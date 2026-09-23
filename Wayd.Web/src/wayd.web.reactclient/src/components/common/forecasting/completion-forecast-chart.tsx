'use client'

import { ColumnConfig } from '@ant-design/charts'
import { WorkItemForecastDto } from '@/src/services/wayd-api'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import dynamic from 'next/dynamic'
import { FC } from 'react'
import useTheme from '../../contexts/theme'
import {
  formatForecastDate,
  formatPercent,
  formatShortForecastDate,
} from './forecast-formatting'

dayjs.extend(utc)

const Column = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Column) as any,
  { ssr: false },
)

export interface CompletionForecastChartProps {
  forecast: WorkItemForecastDto
  /** The date the report measures against, when there is one. */
  targetDate?: Date | string
}

interface ChartDatum {
  label: string
  date: string
  share: number
  cumulative: number
  byTargetDate: boolean
}

/**
 * The share of simulations finishing on each date. Bars on or before the
 * target date are drawn at full strength; later ones are lighter, so the
 * chance of making the date reads as the solid part of the distribution.
 */
const CompletionForecastChart: FC<CompletionForecastChartProps> = ({
  forecast,
  targetDate: target,
}) => {
  const { antDesignChartsTheme, token } = useTheme()

  const targetDate = target ? dayjs.utc(target) : undefined

  const data = [...forecast.histogram]
    .sort((a, b) => dayjs.utc(a.date).diff(dayjs.utc(b.date)))
    .reduce<ChartDatum[]>((points, bucket) => {
      const share = bucket.trials / forecast.trials
      const date = dayjs.utc(bucket.date)
      return [
        ...points,
        {
          label: formatShortForecastDate(bucket.date),
          date: formatForecastDate(bucket.date),
          share: share * 100,
          cumulative: (points.at(-1)?.cumulative ?? 0) + share,
          byTargetDate: !targetDate || !date.isAfter(targetDate, 'day'),
        },
      ]
    }, [])

  const config = {
    theme: antDesignChartsTheme,
    data,
    height: 280,
    xField: 'label',
    yField: 'share',
    axis: {
      x: { title: 'Completion date', labelAutoHide: true },
      y: {
        title: 'Simulations (%)',
        labelFormatter: (value: number) => `${value}%`,
      },
    },
    tooltip: {
      title: (datum: ChartDatum) => datum.date,
      items: [
        {
          name: 'Finish on this date',
          field: 'share',
          valueFormatter: (value: number) => `${value.toFixed(1)}%`,
        },
        {
          name: 'Finished by this date',
          field: 'cumulative',
          valueFormatter: (value: number) => formatPercent(value),
        },
      ],
    },
    style: {
      radiusTopLeft: 4,
      radiusTopRight: 4,
      fill: (datum: ChartDatum) =>
        datum.byTargetDate ? token.colorPrimary : token.colorPrimaryBorder,
    },
  } as ColumnConfig

  return <Column {...(config as any)} />
}

export default CompletionForecastChart
