'use client'

import { Line } from '@/src/components/common/charts'
import { ChartCard } from '@/src/components/common/metrics'
import WaydEmpty from '@/src/components/common/wayd-empty'
import useTheme from '@/src/components/contexts/theme'
import { useChartRemountOnResize } from '@/src/hooks'
import { KpiGoalDto, KpiPointDto } from '@/src/services/api/wayd.schemas'
import {
  formatCalendarDate,
  parseCalendarDate,
} from '@/src/utils/calendar-date'
import { instantTime } from '@/src/utils/instant'
import { FC } from 'react'
import { formatKpiValue } from './kpi-format'

const CHART_HEIGHT = 260

enum KpiSeries {
  Actual = 'Actual',
  Target = 'Target',
  AtRisk = 'At risk',
  Baseline = 'Baseline',
}

interface KpiChartPoint {
  at: Date
  value: number
  series: KpiSeries
}

/**
 * The two ends of a flat reference line: the first and last measurements'
 * dates, or, when those are one day, that measurement's period — widened to
 * the day before for a one-day period.
 */
export function referenceSpan(
  first: KpiPointDto,
  last: KpiPointDto,
  at: (point: KpiPointDto) => Date,
): [Date, Date] {
  const start = at(first)
  const end = at(last)
  if (start.getTime() !== end.getTime()) return [start, end]

  const periodStart = parseCalendarDate(last.periodStart)
  const from = periodStart.isBefore(parseCalendarDate(last.periodEnd))
    ? periodStart
    : parseCalendarDate(last.periodEnd).subtract(1, 'day')
  return [from.toDate(), end]
}

export interface KpiHistoryChartProps {
  /** Every measurement, in any order. */
  values: KpiPointDto[]
  goal: KpiGoalDto
  prefix?: string
  suffix?: string
  isLoading: boolean
}

/**
 * A KPI's measurements over time, against its target, at-risk line and
 * baseline drawn flat across the same span. Each value sits at the end of the
 * period it describes, or at its moment for a point-in-time reading.
 */
const KpiHistoryChart: FC<KpiHistoryChartProps> = ({
  values,
  goal,
  prefix,
  suffix,
  isLoading,
}) => {
  const { antDesignChartsTheme, token } = useTheme()
  const { ref, renderKey } = useChartRemountOnResize()

  const at = (point: KpiPointDto) =>
    point.measuredAt
      ? new Date(instantTime(point.measuredAt))
      : parseCalendarDate(point.periodEnd).toDate()
  const ordered = [...values].sort((a, b) => at(a).getTime() - at(b).getTime())

  const actual: KpiChartPoint[] = ordered.map((p) => ({
    at: at(p),
    value: p.value,
    series: KpiSeries.Actual,
  }))

  // A flat reference line needs two distinct ends. With one measurement they
  // would be the same day, so it spans that measurement's period instead — or
  // the day before it to the day, for a period of a single day.
  const first = ordered[0]
  const last = ordered[ordered.length - 1]
  const span = first && last ? referenceSpan(first, last, at) : []
  const flat = (value: number | null | undefined, series: KpiSeries) =>
    value == null ? [] : span.map((date) => ({ at: date, value, series }))

  const series: { name: KpiSeries; color: string }[] = [
    { name: KpiSeries.Actual, color: token.colorPrimary },
    ...(goal.resolvedTargetValue != null
      ? [{ name: KpiSeries.Target, color: token.colorSuccess }]
      : []),
    ...(goal.resolvedAtRiskValue != null
      ? [{ name: KpiSeries.AtRisk, color: token.colorWarning }]
      : []),
    ...(goal.baselineValue != null
      ? [{ name: KpiSeries.Baseline, color: token.colorTextTertiary }]
      : []),
  ]

  const data: KpiChartPoint[] = [
    ...actual,
    ...flat(goal.resolvedTargetValue, KpiSeries.Target),
    ...flat(goal.resolvedAtRiskValue, KpiSeries.AtRisk),
    ...flat(goal.baselineValue, KpiSeries.Baseline),
  ]

  const config = {
    theme: antDesignChartsTheme,
    autoFit: true,
    height: CHART_HEIGHT,
    data,
    xField: 'at',
    yField: 'value',
    colorField: 'series',
    scale: {
      x: { type: 'time' },
      y: { nice: true },
      color: {
        domain: series.map((s) => s.name),
        range: series.map((s) => s.color),
      },
    },
    style: {
      lineWidth: 2,
      lineDash: (points: KpiChartPoint[]) =>
        points[0]?.series === KpiSeries.Actual ? [0, 0] : [6, 4],
    },
    point: {
      style: { r: 3 },
    },
    axis: {
      x: {
        labelFormatter: (value: Date) =>
          value.toLocaleDateString(undefined, {
            month: 'short',
            day: 'numeric',
          }),
        gridStrokeOpacity: 0.2,
      },
      y: { gridStrokeOpacity: 0.2 },
    },
    legend: {
      color: { layout: { justifyContent: 'center' }, itemMarker: 'square' },
    },
    interaction: { tooltip: { crosshairs: true, shared: true } },
    tooltip: {
      title: (datum: KpiChartPoint) =>
        datum.at.toLocaleDateString(undefined, {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
        }),
      items: [
        {
          channel: 'y',
          valueFormatter: (value: number) =>
            formatKpiValue(value, prefix, suffix),
        },
      ],
    },
  }

  return (
    <ChartCard
      title="History"
      tooltip={
        last
          ? `Each value is placed at the end of its period. Latest period ends ${formatCalendarDate(last.periodEnd)}.`
          : undefined
      }
      loading={isLoading}
      skeletonHeight={CHART_HEIGHT}
    >
      <div ref={ref}>
        {actual.length === 0 ? (
          <WaydEmpty message="No measurements yet" />
        ) : (
          <Line key={renderKey} {...config} />
        )}
      </div>
    </ChartCard>
  )
}

export default KpiHistoryChart
