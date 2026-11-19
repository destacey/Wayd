import {
  KpiAtRiskKind,
  KpiExpressedAs,
  KpiGoalDto,
  KpiMeasurementDto,
  KpiMeasurementWindowDto,
  KpiSummaryDto,
  KpiTargetDirection,
  KpiTargetKind,
  KpiWindowKind,
} from '@/src/services/api/wayd.schemas'
import { formatCalendarDate } from '@/src/utils/calendar-date'
import dayjs from 'dayjs'

/**
 * A raw KPI value with its prefix and suffix, to at most two decimals. The sign
 * goes before the prefix ("-$5"), and a suffix that starts with a symbol
 * ("%", "°") sits against the number while a word ("days") is spaced.
 */
export function formatKpiValue(
  value: number | null | undefined,
  prefix?: string | null,
  suffix?: string | null,
): string {
  if (value == null) return '—'
  const number = Math.abs(value).toLocaleString(undefined, {
    maximumFractionDigits: 2,
  })
  // Signed after rounding, so a value that rounds to zero never reads "-0".
  const sign = value < 0 && Math.round(Math.abs(value) * 100) > 0 ? '-' : ''
  const unit = !suffix
    ? ''
    : /^[\p{L}\p{N}]/u.test(suffix)
      ? ` ${suffix}`
      : suffix
  return `${sign}${prefix ?? ''}${number}${unit}`
}

/** A percentage, signed when asked, to at most one decimal. */
export function formatPercent(
  value: number | null | undefined,
  signed = false,
): string {
  if (value == null) return '—'
  const text = value.toLocaleString(undefined, { maximumFractionDigits: 1 })
  return `${signed && value > 0 ? '+' : ''}${text}%`
}

/** What a KPI's scorecard headline reads, by the reading the KPI is shown as. */
export interface KpiHeadline {
  label: string
  text: string
}

/**
 * The scorecard headline for a KPI: its latest value, its change from
 * baseline, or its progress to target, as the KPI chooses. A reading that
 * cannot be worked out — no baseline yet — falls back to the raw value, so the
 * card never shows a blank where a number was measured.
 */
export function kpiHeadline(kpi: KpiSummaryDto): KpiHeadline {
  const { reading } = kpi
  // A single value is not an average, though the KPI asks for one.
  const averaged = (reading.averagedCount ?? 0) > 1
  const of = averaged ? ` (average of last ${reading.averagedCount})` : ''
  const value = {
    label: averaged ? `Average of last ${reading.averagedCount}` : 'Latest',
    text: formatKpiValue(reading.value, kpi.prefix, kpi.suffix),
  }
  if (reading.value == null) return value

  switch (kpi.expressedAs) {
    case KpiExpressedAs.ChangeFromBaseline:
      return reading.changeFromBaselinePercent == null
        ? value
        : {
            label: `Change from baseline${of}`,
            text: formatPercent(reading.changeFromBaselinePercent, true),
          }
    case KpiExpressedAs.ProgressToTarget:
      return reading.progressToTarget == null
        ? value
        : {
            label: `Progress to target${of}`,
            text: formatPercent(reading.progressToTarget * 100),
          }
    default:
      return value
  }
}

/** The target as entered, such as "20 days" or "−25% from baseline (15 days)". */
export function describeTarget(
  goal: KpiGoalDto,
  prefix?: string | null,
  suffix?: string | null,
): string {
  if (goal.targetKind === KpiTargetKind.Absolute)
    return formatKpiValue(goal.targetValue, prefix, suffix)

  const sign = goal.direction === KpiTargetDirection.Increase ? '+' : '−'
  const resolved =
    goal.resolvedTargetValue != null
      ? ` (${formatKpiValue(goal.resolvedTargetValue, prefix, suffix)})`
      : ''
  return `${sign}${formatPercent(goal.targetValue)} from baseline${resolved}`
}

/**
 * The change from baseline, signed, with its percentage when there is one — a
 * zero baseline has none.
 */
export function describeChange(
  change: number,
  percent: number | null | undefined,
  prefix?: string | null,
  suffix?: string | null,
): string {
  const value = `${change > 0 ? '+' : ''}${formatKpiValue(change, prefix, suffix)}`
  return percent == null ? value : `${value} (${formatPercent(percent, true)})`
}

/**
 * What the at-risk threshold is labelled: a value short of it is at risk, which
 * is below it when higher is better and above it when lower is.
 */
export function atRiskLabel(direction: KpiTargetDirection): string {
  return direction === KpiTargetDirection.Decrease
    ? 'At risk above'
    : 'At risk below'
}

/**
 * What span each of a KPI's values covers, such as "Per period", "Since Feb 16,
 * 2026" or "Last 90 days".
 */
export function describeWindow(
  measurementWindow: KpiMeasurementWindowDto,
): string {
  const { kind, anchorDate, rollingCount } = measurementWindow
  switch (kind) {
    case KpiWindowKind.Cumulative:
      return anchorDate
        ? `Since ${formatCalendarDate(anchorDate)}`
        : 'Cumulative'
    case KpiWindowKind.Rolling:
      return rollingCount != null
        ? `Last ${rollingCount} ${rollingCount === 1 ? 'day' : 'days'}`
        : 'Rolling'
    case KpiWindowKind.PointInTime:
      return 'Point in time'
    default:
      return 'Per period'
  }
}

/** The parts of a measurement that say when it was taken. */
export type KpiMeasurementTiming = Pick<
  KpiMeasurementDto,
  'periodStart' | 'periodEnd' | 'measuredAt'
>

/**
 * When a value was taken, as its KPI's window reads it: the period, the day it
 * was taken as of, or the moment in the viewer's own time zone.
 */
export function describeMeasurementTime(
  measurement: KpiMeasurementTiming,
  measurementWindow: KpiMeasurementWindowDto,
): string {
  if (measurement.measuredAt)
    return dayjs(measurement.measuredAt).format('MMM D, YYYY h:mm A')
  if (
    measurementWindow.kind === KpiWindowKind.Cumulative ||
    measurementWindow.kind === KpiWindowKind.Rolling
  )
    return `As of ${formatCalendarDate(measurement.periodEnd)}`
  return `${formatCalendarDate(measurement.periodStart)} – ${formatCalendarDate(measurement.periodEnd)}`
}

/** The at-risk threshold as entered, or null when there is none. */
export function describeAtRisk(
  goal: KpiGoalDto,
  prefix?: string | null,
  suffix?: string | null,
): string | null {
  if (goal.atRiskKind == null || goal.atRiskValue == null) return null
  if (goal.atRiskKind === KpiAtRiskKind.Absolute)
    return formatKpiValue(goal.atRiskValue, prefix, suffix)

  const resolved =
    goal.resolvedAtRiskValue != null
      ? ` (${formatKpiValue(goal.resolvedAtRiskValue, prefix, suffix)})`
      : ''
  return `${formatPercent(goal.atRiskValue)} of the way to target${resolved}`
}
