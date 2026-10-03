import {
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
} from '@/src/utils/calendar-date'

export const formatForecastDate = (value: CalendarDate) =>
  formatCalendarDate(value)

export const formatShortForecastDate = (value: CalendarDate) =>
  formatCalendarDate(value, 'MMM D')

export const formatPercent = (share: number) => `${Math.round(share * 100)}%`

/**
 * The share of trials (0 to 1) finishing on or before a date. The histogram
 * holds every trial that finished, so this matches the API's
 * ChanceOfFinishingByTargetDate without re-running the forecast; trials
 * beyond the horizon count against it through `trials`.
 */
export const chanceOfFinishingBy = (
  histogram: { date: CalendarDate; trials: number }[],
  trials: number,
  targetDate: CalendarDate,
): number => {
  if (trials === 0) return 0
  const finished = histogram
    .filter((bucket) => compareCalendarDates(bucket.date, targetDate) <= 0)
    .reduce((sum, bucket) => sum + bucket.trials, 0)
  return finished / trials
}

/** Mirrors the API's WorkItemForecastOutcome. */
export const ForecastOutcome = {
  Forecast: 1,
  AlreadyDone: 2,
  NotEnoughHistory: 3,
  BlockedByDependency: 4,
  CannotForecast: 5,
  NoRemainingWork: 6,
} as const
