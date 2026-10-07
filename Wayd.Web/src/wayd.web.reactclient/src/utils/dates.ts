import { calendarDaysBetween, CalendarDate } from './calendar-date'

/**
 * Calculates the number of days remaining from a reference date (defaults to today)
 * to the given end date, counted in calendar days on the viewer's calendar.
 *
 * @param endDate - The end date to calculate the remaining days for.
 * @param referenceDate - Optional reference date used instead of the current date.
 * @returns The number of days remaining. A positive number indicates a future date,
 *          zero indicates the same day, and a negative number indicates a past date.
 */
export function daysRemaining(
  endDate: CalendarDate | Date,
  referenceDate?: CalendarDate | Date,
): number {
  return calendarDaysBetween(referenceDate ?? new Date(), endDate)
}

/**
 * Which day of a period the reference date (today by default) is, counting the
 * first and last days inclusively — "day 9 of 14". Day 0 before the period
 * starts; capped at the last day after it ends. Every view of a period's
 * progress reads it from here, so a timeline and a countdown always agree.
 */
export function dayOfPeriod(
  startDate: CalendarDate | Date,
  endDate: CalendarDate | Date,
  referenceDate?: CalendarDate | Date,
): { currentDay: number; totalDays: number } {
  const totalDays = calendarDaysBetween(startDate, endDate) + 1
  const day = calendarDaysBetween(startDate, referenceDate ?? new Date()) + 1
  return {
    currentDay: Math.min(Math.max(day, 0), Math.max(totalDays, 0)),
    totalDays,
  }
}

/**
 * The share of a period's days reached, as {@link dayOfPeriod} counts them:
 * on day 9 of 14, 64%. 0 before the period starts, 100 after it ends.
 */
export function percentageElapsed(
  startDate: CalendarDate | Date,
  endDate: CalendarDate | Date,
  referenceDate?: CalendarDate | Date,
): number {
  const { currentDay, totalDays } = dayOfPeriod(
    startDate,
    endDate,
    referenceDate,
  )
  return totalDays > 0 ? (currentDay / totalDays) * 100 : 0
}

/**
 * An instant as the clock read in `timeZone`, an IANA id such as a team's,
 * with the zone's abbreviation so the reader knows which clock it is. Takes
 * the ISO string a `Date`-typed field really holds as well as a `Date`.
 */
export function formatInstantInZone(
  instant: Date | string,
  timeZone: string,
): string {
  return new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'short',
  }).format(new Date(instant))
}
