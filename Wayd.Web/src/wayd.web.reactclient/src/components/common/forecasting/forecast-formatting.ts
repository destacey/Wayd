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

/** Mirrors the API's WorkItemForecastOutcome. */
export const ForecastOutcome = {
  Forecast: 1,
  AlreadyDone: 2,
  NotEnoughHistory: 3,
  BlockedByDependency: 4,
  CannotForecast: 5,
  NoRemainingWork: 6,
} as const
