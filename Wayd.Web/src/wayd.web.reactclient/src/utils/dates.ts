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
 * Calculates the percentage of days elapsed between a start date and end date,
 * counted in whole calendar days like daysRemaining.
 *
 * @param startDate - The start date of the period.
 * @param endDate - The end date of the period.
 * @param referenceDate - Optional reference date used instead of the current date.
 * @returns The percentage elapsed (0-100). Returns 0 if the period hasn't started,
 *          capped at 100 if past the end date.
 */
export function percentageElapsed(
  startDate: CalendarDate | Date,
  endDate: CalendarDate | Date,
  referenceDate?: CalendarDate | Date,
): number {
  const totalDays = calendarDaysBetween(startDate, endDate)
  const elapsedDays = Math.max(
    0,
    calendarDaysBetween(startDate, referenceDate ?? new Date()),
  )

  if (totalDays <= 0) return 0

  const percentage = (elapsedDays / totalDays) * 100
  return Math.min(100, percentage)
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
