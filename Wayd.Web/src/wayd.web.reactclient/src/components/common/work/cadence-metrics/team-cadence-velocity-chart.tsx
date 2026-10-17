'use client'

import { ColumnConfig } from '@ant-design/charts'
import dynamic from 'next/dynamic'
import { FC } from 'react'
import { TeamCadenceMetricsDto } from '@/src/services/wayd-api'
import { formatCalendarDate } from '@/src/utils/calendar-date'
import useTheme from '../../../contexts/theme'
import {
  formatVelocity,
  periodDates,
  periodNote,
  periodVelocity,
} from './team-cadence-metrics-formatting'

const Column = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Column) as any,
  { ssr: false },
)

export interface TeamCadenceVelocityChartProps {
  metrics: TeamCadenceMetricsDto
  byCount: boolean
  /** What the values count, for the axis and tooltip: "story points", "work items". */
  measure: string
}

interface ChartDatum {
  period: string
  dates: string
  velocity: number
  note: string | null
}

/**
 * The team's velocity in each period, oldest on the left. The current and
 * partial periods are drawn as outlined bars labelled with why, because they
 * are not averaged.
 */
const TeamCadenceVelocityChart: FC<TeamCadenceVelocityChartProps> = ({
  metrics,
  byCount,
  measure,
}) => {
  const { antDesignChartsTheme, token } = useTheme()

  const data: ChartDatum[] = [...metrics.periods]
    .reverse()
    .flatMap((period) => {
      const velocity = periodVelocity(period, metrics, byCount)
      return velocity === null
        ? []
        : [
            {
              period: formatCalendarDate(period.start, 'MMM D'),
              dates: periodDates(period),
              velocity,
              note: periodNote(period),
            },
          ]
    })

  const config = {
    theme: antDesignChartsTheme,
    data,
    height: 240,
    xField: 'period',
    yField: 'velocity',
    axis: {
      x: { title: 'Period starting', labelAutoHide: true },
      y: { title: `Completed (${measure})` },
    },
    tooltip: {
      title: (datum: ChartDatum) => datum.dates,
      items: [
        {
          name: 'Completed',
          field: 'velocity',
          valueFormatter: (value: number) =>
            `${formatVelocity(value)} ${measure}`,
        },
        {
          name: 'Note',
          field: 'note',
          valueFormatter: (value: string | null) => value ?? 'Averaged',
        },
      ],
    },
    style: {
      radiusTopLeft: 4,
      radiusTopRight: 4,
      // A pale fill alone would vanish on the dark surface; the full-strength
      // outline keeps an unaveraged bar's edge visible in both themes.
      fill: token.colorPrimary,
      fillOpacity: (datum: ChartDatum) => (datum.note ? 0.3 : 1),
      stroke: token.colorPrimary,
      lineWidth: (datum: ChartDatum) => (datum.note ? 1.5 : 0),
    },
    label: {
      text: (datum: ChartDatum) => datum.note ?? '',
      position: 'top',
      style: { fill: token.colorTextSecondary, fontSize: 11 },
    },
  } as ColumnConfig

  return <Column {...(config as any)} />
}

export default TeamCadenceVelocityChart
