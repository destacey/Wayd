import dayjs, { Dayjs } from 'dayjs'

/**
 * A day on the calendar as the API publishes it, `"YYYY-MM-DD"` (a NodaTime
 * `LocalDate`). It names a day, not a moment, so it has no time zone: show it
 * as written and never convert it to the viewer's zone.
 *
 * Never parse one with `new Date(value)`, which reads it as UTC midnight and
 * so lands on the previous day anywhere west of UTC.
 */
export type CalendarDate = string

const ISO_FORMAT = 'YYYY-MM-DD'
const DISPLAY_FORMAT = 'MMM D, YYYY'
const MS_PER_DAY = 86_400_000
const CALENDAR_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/

/**
 * Local midnight of the day, for date math and date pickers. dayjs reads an
 * ISO date with no time or offset as local, unlike `Date`.
 */
export function parseCalendarDate(value: CalendarDate): Dayjs {
  return dayjs(value)
}

/** The day a picker value or local date falls on, in the viewer's calendar. */
export function toCalendarDate(value: Dayjs | Date): CalendarDate {
  return dayjs(value).format(ISO_FORMAT)
}

export function formatCalendarDate(
  value: CalendarDate | null | undefined,
  format: string = DISPLAY_FORMAT,
): string {
  return value ? parseCalendarDate(value).format(format) : ''
}

/**
 * The day an instant falls on in `timeZone`, an IANA id such as a team's,
 * whatever the viewer's zone. Takes the ISO string a `Date`-typed field really
 * holds as well as a `Date`.
 */
export function calendarDateInZone(
  instant: Date | string,
  timeZone: string,
): CalendarDate {
  // en-CA formats a date as YYYY-MM-DD.
  return new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date(instant))
}

/** Today in the viewer's calendar. */
export function todayCalendarDate(): CalendarDate {
  return toCalendarDate(new Date())
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
  from: CalendarDate | Date,
  to: CalendarDate | Date,
): number {
  return dayNumber(to) - dayNumber(from)
}

function dayNumber(value: CalendarDate | Date): number {
  if (typeof value === 'string' && CALENDAR_DATE_PATTERN.test(value)) {
    const [year, month, day] = value.split('-').map(Number)
    return Date.UTC(year, month - 1, day) / MS_PER_DAY
  }
  // Anything else is an instant. A `Date`-typed instant still arrives from
  // the API as an ISO string, and its date prefix is the UTC day, not the
  // viewer's.
  const instant = typeof value === 'string' ? new Date(value) : value
  return (
    Date.UTC(instant.getFullYear(), instant.getMonth(), instant.getDate()) /
    MS_PER_DAY
  )
}
