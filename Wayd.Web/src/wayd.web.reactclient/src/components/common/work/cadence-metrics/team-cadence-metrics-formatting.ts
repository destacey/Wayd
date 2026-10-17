import {
  CadenceVelocitySummaryDto,
  SizingMethod,
  TeamCadenceMetricsDto,
  TeamCadencePeriodDto,
} from '@/src/services/wayd-api'
import { formatCalendarDate } from '@/src/utils/calendar-date'

/** The bounds the API holds a period window to. */
export const MIN_PERIOD_WINDOW = 3
export const MAX_PERIOD_WINDOW = 12

/** What stands in for a value there is none of. */
export const NO_VALUE = '—'

export const VELOCITY_TOOLTIP =
  'Work completed in each period of the team’s cadence, by done date. A cadence is for reporting only, so there is no commitment, predictability or say/do.'

/** "Oct 5, 2026 – Oct 18, 2026". */
export const periodDates = (period: { start: string; end: string }) =>
  `${formatCalendarDate(period.start)} – ${formatCalendarDate(period.end)}`

/** "1 period", "5 periods". */
export const periodCountText = (count: number): string =>
  `${count} ${count === 1 ? 'period' : 'periods'}`

/**
 * An average velocity as shown, rounded to a whole number: an average of a few
 * periods is a planning figure, and a decimal reads as more precision than it
 * carries.
 */
export const formatVelocityAverage = (
  summary: CadenceVelocitySummaryDto | undefined,
): string => (summary ? Math.round(summary.average).toLocaleString() : NO_VALUE)

/** One period's velocity as shown. */
export const formatVelocity = (value: number | null | undefined): string =>
  value === null || value === undefined
    ? NO_VALUE
    : value.toLocaleString(undefined, { maximumFractionDigits: 1 })

/** The spread across the periods, as the lowest and highest. */
export const formatVelocityRange = (
  summary: CadenceVelocitySummaryDto | undefined,
): string | undefined =>
  summary
    ? `Range ${formatVelocity(summary.min)}–${formatVelocity(summary.max)}`
    : undefined

/** Whether the figures shown are item counts rather than estimates. */
export const showsCount = (
  metrics: TeamCadenceMetricsDto | undefined,
  byCount: boolean,
) => byCount || !metrics || metrics.sizingMethod === SizingMethod.Count

/** The summary in the unit shown. */
export const velocitySummary = (
  metrics: TeamCadenceMetricsDto,
  byCount: boolean,
): CadenceVelocitySummaryDto | undefined =>
  (showsCount(metrics, byCount) ? metrics.byCount : metrics.byEstimate) ??
  undefined

/**
 * A period's velocity in the unit shown. Null by estimate for a period measured
 * in another sizing method than the report's: its estimates are in a different
 * unit, so they are neither shown beside the others nor averaged with them.
 */
export const periodVelocity = (
  period: TeamCadencePeriodDto,
  metrics: TeamCadenceMetricsDto,
  byCount: boolean,
): number | null => {
  if (showsCount(metrics, byCount)) return period.completedCount
  return period.sizingMethod === metrics.sizingMethod
    ? period.completedEstimate
    : null
}

/** Why a period is left out of the averages, or null when it is in them. */
export const periodNote = (period: TeamCadencePeriodDto): string | null => {
  if (period.isCurrent) return 'In progress'
  if (period.isPartial) return 'Partial'
  return null
}

export const PERIOD_NOTE_DESCRIPTIONS: Record<string, string> = {
  'In progress':
    'The period has not ended, so its work is still coming in. It is left out of the averages.',
  Partial:
    'The period was cut short where an operating model started or ended inside it. It is left out of the averages.',
}
