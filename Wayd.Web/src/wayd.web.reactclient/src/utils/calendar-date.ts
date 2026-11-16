import dayjs, { Dayjs } from 'dayjs'
import type { Instant } from './instant'

/**
 * A day on the calendar as the API publishes it, `"YYYY-MM-DD"` (a NodaTime
 * `LocalDate`). It names a day, not a moment, so it has no time zone: show it
 * as written and never convert it to the viewer's zone.
 *
 * Never parse one with `new Date(value)`, which reads it as UTC midnight and
 * so lands on the previous day anywhere west of UTC.
 *
 * Branded so that it cannot be mixed up with an {@link Instant} or an arbitrary
 * string: build one with the helpers here, or {@link asCalendarDate} for a
 * value written out in full. The generated client's `format: date` fields have
 * this exact type (see `orval.config.ts`).
 */
export type CalendarDate = string & { readonly __brand: 'CalendarDate' }

const ISO_FORMAT = 'YYYY-MM-DD'
const DISPLAY_FORMAT = 'MMM D, YYYY'
const MS_PER_DAY = 86_400_000
const CALENDAR_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/

/**
 * A `"YYYY-MM-DD"` string as a calendar date, for a day written out in full
 * (a constant, a URL parameter, a test). Throws for anything else.
 */
export function asCalendarDate(value: string): CalendarDate {
  if (!CALENDAR_DATE_PATTERN.test(value)) {
    throw new RangeError(`"${value}" is not a YYYY-MM-DD calendar date.`)
  }
  return value as CalendarDate
}

/**
 * Local midnight of the day, for date math and date pickers. dayjs reads an
 * ISO date with no time or offset as local, unlike `Date`.
 */
export function parseCalendarDate(value: CalendarDate): Dayjs {
  return dayjs(value)
}

/** The day a picker value or local date falls on, in the viewer's calendar. */
export function toCalendarDate(value: Dayjs | Date): CalendarDate {
  return dayjs(value).format(ISO_FORMAT) as CalendarDate
}

export function formatCalendarDate(
  value: CalendarDate | null | undefined,
  format: string = DISPLAY_FORMAT,
): string {
  return value ? parseCalendarDate(value).format(format) : ''
}

/**
 * The day an instant falls on in `timeZone`, an IANA id such as a team's,
 * whatever the viewer's zone.
 */
export function calendarDateInZone(
  instant: Date | Instant,
  timeZone: string,
): CalendarDate {
  // en-CA formats a date as YYYY-MM-DD.
  return new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(instant)) as CalendarDate
}

/** Today in the viewer's calendar. */
export function todayCalendarDate(): CalendarDate {
  return toCalendarDate(new Date())
}

/** Today in `timeZone`, an IANA id such as a team's; the viewer's today when absent. */
export function todayInZone(timeZone: string | undefined): CalendarDate {
  return timeZone
    ? calendarDateInZone(new Date(), timeZone)
    : todayCalendarDate()
}

/**
 * Orders calendar dates. ISO dates sort as plain strings, so this needs no
 * parsing and no locale collation; missing values sort first.
 */
export function compareCalendarDates(
  a: CalendarDate | null | undefined,
  b: CalendarDate | null | undefined,
): number {
  const left = a ?? ''
  const right = b ?? ''
  return left < right ? -1 : left > right ? 1 : 0
}

/**
 * Whole days from `from` to `to`, counted on the calendar: a calendar date is
 * its own day, and an instant (a `Date`, or the ISO string a `Date`-typed
 * field really holds) is the day it falls on for the viewer. Counted
 * from the day's numbers rather than milliseconds, so a DST change between
 * them cannot shift the result.
 */
export function calendarDaysBetween(
  from: CalendarDate | Instant | Date,
  to: CalendarDate | Instant | Date,
): number {
  return dayNumber(to) - dayNumber(from)
}

/**
 * The calendar date `days` after `value`, or before it when negative. Counted
 * from the day's numbers, so a DST change in between cannot shift the result.
 */
export function addCalendarDays(
  value: CalendarDate,
  days: number,
): CalendarDate {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(Date.UTC(year, month - 1, day + days))
    .toISOString()
    .slice(0, 10) as CalendarDate
}

/**
 * Local midnight of the day as epoch milliseconds, for components such as the
 * timeline that place days on a millisecond axis. Built from the day's
 * numbers, so it cannot land on the day before as `new Date(value)` does.
 */
export function startOfCalendarDate(value: CalendarDate): number {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day).getTime()
}

function dayNumber(value: CalendarDate | Instant | Date): number {
  if (typeof value === 'string' && CALENDAR_DATE_PATTERN.test(value)) {
    const [year, month, day] = value.split('-').map(Number)
    return Date.UTC(year, month - 1, day) / MS_PER_DAY
  }
  // Anything else is an instant, whose day is the viewer's: an ISO string's
  // date prefix is the UTC day instead.
  const instant = typeof value === 'string' ? new Date(value) : value
  return (
    Date.UTC(instant.getFullYear(), instant.getMonth(), instant.getDate()) /
    MS_PER_DAY
  )
}
