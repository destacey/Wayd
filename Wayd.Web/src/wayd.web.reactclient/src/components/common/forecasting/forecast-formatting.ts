import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'

dayjs.extend(utc)

/**
 * Forecast dates are UTC calendar dates; formatting them in local time would
 * shift them a day for anyone behind UTC.
 */
export const formatForecastDate = (value: Date | string) =>
  dayjs.utc(value).format('MMM D, YYYY')

export const formatShortForecastDate = (value: Date | string) =>
  dayjs.utc(value).format('MMM D')

export const formatPercent = (share: number) => `${Math.round(share * 100)}%`

/**
 * The share of trials (0 to 1) finishing on or before a date. The histogram
 * holds every trial that finished, so this matches the API's
 * ChanceOfFinishingByTargetDate without re-running the forecast; trials
 * beyond the horizon count against it through `trials`.
 */
export const chanceOfFinishingBy = (
  histogram: { date: Date | string; trials: number }[],
  trials: number,
  targetDate: Date | string,
): number => {
  if (trials === 0) return 0
  const target = dayjs.utc(targetDate)
  const finished = histogram
    .filter((bucket) => !dayjs.utc(bucket.date).isAfter(target, 'day'))
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
