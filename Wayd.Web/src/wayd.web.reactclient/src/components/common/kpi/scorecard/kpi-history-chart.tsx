'use client'

import { Line } from '@/src/components/common/charts'
import { ChartCard } from '@/src/components/common/metrics'
import WaydEmpty from '@/src/components/common/wayd-empty'
import useTheme from '@/src/components/contexts/theme'
import { useChartRemountOnResize } from '@/src/hooks'
import {
  KpiCheckpointDto,
  KpiGoalDto,
  KpiPointDto,
} from '@/src/services/api/wayd.schemas'
import {
  formatCalendarDate,
  parseCalendarDate,
} from '@/src/utils/calendar-date'
import { instantTime } from '@/src/utils/instant'
import dayjs from 'dayjs'
import { FC } from 'react'
import { formatKpiValue } from './kpi-format'

const CHART_HEIGHT = 260

export enum KpiSeries {
  Actual = 'Actual',
  Target = 'Target',
  AtRisk = 'At risk',
  Baseline = 'Baseline',
  Plan = 'Plan',
  PlanAtRisk = 'Plan at risk',
}

/**
 * Each series' dash: measurements solid, the KPI's flat lines dashed, the plan
 * dash-dotted and its at-risk line dotted, so no two lines rely on colour alone.
 */
const LINE_DASH: Record<KpiSeries, number[]> = {
  [KpiSeries.Actual]: [0, 0],
  [KpiSeries.Target]: [6, 4],
  [KpiSeries.AtRisk]: [6, 4],
  [KpiSeries.Baseline]: [6, 4],
  [KpiSeries.Plan]: [8, 3, 2, 3],
  [KpiSeries.PlanAtRisk]: [2, 3],
}

export interface KpiChartPoint {
  at: Date
  /** Null breaks the line, where a stretch of the plan has no at-risk value. */
  value: number | null
  series: KpiSeries
}

/**
 * A checkpoint plan as stepped lines: each checkpoint's planned value held flat
 * from the day after the checkpoint before (or `from`, for the first) to its
 * own date, which is the stretch a measurement is judged against it. Its
 * at-risk line steps the same way, broken where a checkpoint has none.
 */
export function planSteps(
  checkpoints: KpiCheckpointDto[],
  from: Date | undefined,
): KpiChartPoint[] {
  const ordered = [...checkpoints].sort((a, b) =>
    a.date < b.date ? -1 : a.date > b.date ? 1 : 0,
  )
  const plan: KpiChartPoint[] = []
  const atRisk: KpiChartPoint[] = []

  ordered.forEach((checkpoint, index) => {
    const end = parseCalendarDate(checkpoint.date)
    const previous = ordered[index - 1]
    const proposed = previous
      ? parseCalendarDate(previous.date).add(1, 'day')
      : from
        ? dayjs(from)
        : end.subtract(1, 'day')
    // A stretch needs two distinct ends to draw.
    const start = proposed.isBefore(end, 'day')
      ? proposed
      : end.subtract(1, 'day')

    plan.push(
      {
        at: start.toDate(),
        value: checkpoint.plannedValue,
        series: KpiSeries.Plan,
      },
      {
        at: end.toDate(),
        value: checkpoint.plannedValue,
        series: KpiSeries.Plan,
      },
    )

    if (checkpoint.atRiskValue == null) {
      atRisk.push({
        at: start.toDate(),
        value: null,
        series: KpiSeries.PlanAtRisk,
      })
    } else {
      atRisk.push(
        {
          at: start.toDate(),
          value: checkpoint.atRiskValue,
          series: KpiSeries.PlanAtRisk,
        },
        {
          at: end.toDate(),
          value: checkpoint.atRiskValue,
          series: KpiSeries.PlanAtRisk,
        },
      )
    }
  })

  return atRisk.some((p) => p.value != null) ? [...plan, ...atRisk] : plan
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

/** A span stretched to cover every point of the plan as well. */
export function widenToPlan(
  [start, end]: [Date, Date],
  plan: KpiChartPoint[],
): [Date, Date] {
  const times = plan.map((p) => p.at.getTime())
  if (times.length === 0) return [start, end]
  return [
    new Date(Math.min(start.getTime(), ...times)),
    new Date(Math.max(end.getTime(), ...times)),
  ]
}

export interface KpiHistoryChartProps {
  /** Every measurement, in any order. */
  values: KpiPointDto[]
  goal: KpiGoalDto
  /** The checkpoint plan, drawn as stepped lines; empty or absent for none. */
  checkpoints?: KpiCheckpointDto[]
  prefix?: string
  suffix?: string
  isLoading: boolean
}

/**
 * A KPI's measurements over time, against its target, at-risk line and
 * baseline drawn flat across the same span, and its checkpoint plan as steps.
 * Each value sits at the end of the period it describes, or at its moment for
 * a point-in-time reading.
 */
const KpiHistoryChart: FC<KpiHistoryChartProps> = ({
  values,
  goal,
  checkpoints = [],
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
  // Point-in-time readings can fall on one day, so their time tells them apart.
  const timed = ordered.some((p) => p.measuredAt)

  const actual: KpiChartPoint[] = ordered.map((p) => ({
    at: at(p),
    value: p.value,
    series: KpiSeries.Actual,
  }))

  const first = ordered[0]
  const last = ordered[ordered.length - 1]

  // The plan starts where the baseline was taken, or with the first value.
  const planFrom = goal.baselineWindowEnd
    ? parseCalendarDate(goal.baselineWindowEnd).toDate()
    : first
      ? at(first)
      : undefined
  const plan = planSteps(checkpoints, planFrom)
  const hasPlanAtRisk = plan.some((p) => p.series === KpiSeries.PlanAtRisk)

  // A flat reference line needs two distinct ends. With one measurement they
  // would be the same day, so it spans that measurement's period instead — or
  // the day before it to the day, for a period of a single day. It reaches as
  // far as the plan does, so the target does not seem to stop short of it.
  const span =
    first && last ? widenToPlan(referenceSpan(first, last, at), plan) : []
  const flat = (value: number | null | undefined, series: KpiSeries) =>
    value == null ? [] : span.map((date) => ({ at: date, value, series }))

  // The plan takes a hue of its own (colorInfo is the primary blue in the
  // default theme), and each line its own dash, so they part without colour.
  const series: { name: KpiSeries; color: string }[] = [
    { name: KpiSeries.Actual, color: token.colorPrimary },
    ...(plan.length > 0 ? [{ name: KpiSeries.Plan, color: token.purple }] : []),
    ...(hasPlanAtRisk
      ? [{ name: KpiSeries.PlanAtRisk, color: token.colorWarning }]
      : []),
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
    ...plan,
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
        points[0] ? LINE_DASH[points[0].series] : LINE_DASH[KpiSeries.Actual],
    },
    point: {
      style: {
        r: (datum: KpiChartPoint) =>
          datum.series === KpiSeries.Actual ? 3 : 0,
      },
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
        datum.at.toLocaleString(undefined, {
          year: 'numeric',
          month: 'short',
          day: 'numeric',
          ...(timed ? { hour: 'numeric', minute: '2-digit' } : {}),
        }),
      items: [
        {
          channel: 'y',
          // Null where the plan's at-risk line breaks, which reads as a dash.
          valueFormatter: (value: number | null) =>
            formatKpiValue(value, prefix, suffix),
        },
      ],
    },
  }

  return (
    <ChartCard
      title="History"
      tooltip={
        !last
          ? undefined
          : last.measuredAt
            ? `Each value is placed at the moment it describes. Latest taken ${dayjs(last.measuredAt).format('MMM D, YYYY h:mm A')}.`
            : `Each value is placed at the end of its period. Latest period ends ${formatCalendarDate(last.periodEnd)}.`
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
