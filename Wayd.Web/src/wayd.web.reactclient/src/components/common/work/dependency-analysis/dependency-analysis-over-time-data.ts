import { DependencyAnalysisPeriodDto } from '@/src/services/wayd-api'
import {
  addCalendarDays,
  calendarDaysBetween,
  formatCalendarDate,
} from '@/src/utils/calendar-date'

export interface OpenDatum {
  period: string
  dates: string
  open: number
}

export interface FlowDatum {
  period: string
  dates: string
  series: 'Created' | 'Closed'
  count: number
}

const lengthOf = (period: DependencyAnalysisPeriodDto) =>
  calendarDaysBetween(period.start, period.end) + 1

/**
 * Whether a period is shorter than the others: a week or two-week period cut short by the end of the range, or
 * a month cut short at either end. Its counts cover fewer days, so they read low.
 */
export const isPartialPeriod = (
  period: DependencyAnalysisPeriodDto,
  periods: DependencyAnalysisPeriodDto[],
): boolean => {
  const longest = Math.max(...periods.map(lengthOf))
  // The server splits ranges over 184 days into calendar months; shorter ones into 7 or 14 days.
  if (longest > 14) {
    const endsTheMonth =
      formatCalendarDate(addCalendarDays(period.end, 1), 'D') === '1'
    return formatCalendarDate(period.start, 'D') !== '1' || !endsTheMonth
  }
  return lengthOf(period) < longest
}

/** The period's axis label: its first day, marked when it is partial. */
export const periodLabel = (
  period: DependencyAnalysisPeriodDto,
  periods: DependencyAnalysisPeriodDto[],
) => {
  const label = formatCalendarDate(period.start, 'MMM D, YYYY')
  return isPartialPeriod(period, periods) ? `${label} (partial)` : label
}

/** The days a period covers, for its tooltip. */
export const periodDates = (period: DependencyAnalysisPeriodDto) =>
  period.start === period.end
    ? formatCalendarDate(period.start, 'MMM D, YYYY')
    : `${formatCalendarDate(period.start, 'MMM D')} – ${formatCalendarDate(period.end, 'MMM D, YYYY')}`

/** One point per period: the dependencies open at its end. */
export const buildOpenData = (
  periods: DependencyAnalysisPeriodDto[],
): OpenDatum[] =>
  periods.map((p) => ({
    period: periodLabel(p, periods),
    dates: periodDates(p),
    open: p.open,
  }))

/** Two bars per period: the dependencies created in it, then those closed in it. */
export const buildFlowData = (
  periods: DependencyAnalysisPeriodDto[],
): FlowDatum[] =>
  periods.flatMap((p) => {
    const period = periodLabel(p, periods)
    const dates = periodDates(p)
    return [
      { period, dates, series: 'Created' as const, count: p.created },
      { period, dates, series: 'Closed' as const, count: p.closed },
    ]
  })

/** A sentence describing the chart for screen readers, which cannot read the canvas. */
export const describeOpen = (periods: DependencyAnalysisPeriodDto[]) =>
  periods.length === 0
    ? 'No periods.'
    : `Open dependencies at the end of each period: ${periods
        .map((p) => `${periodDates(p)}, ${p.open}`)
        .join('; ')}.`

/** A sentence describing the chart for screen readers, which cannot read the canvas. */
export const describeFlow = (periods: DependencyAnalysisPeriodDto[]) =>
  periods.length === 0
    ? 'No periods.'
    : `Dependencies created and closed in each period: ${periods
        .map((p) => `${periodDates(p)}, ${p.created} created, ${p.closed} closed`)
        .join('; ')}.`
