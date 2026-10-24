const MINUTE = 60
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR

const plural = (count: number, unit: string) =>
  `${count} ${unit}${count === 1 ? '' : 's'}`

/**
 * A time spent in a status, in the largest whole unit that fits: "41 days", "3 hours", "12 minutes".
 *
 * Whole units, rounded down, because the figure answers "how long has this sat here?" — a status
 * entered 23 hours ago has not yet been there a day.
 */
export const formatTimeInStatus = (seconds: number): string => {
  if (seconds < MINUTE) return 'under a minute'
  if (seconds < HOUR) return plural(Math.floor(seconds / MINUTE), 'minute')
  if (seconds < DAY) return plural(Math.floor(seconds / HOUR), 'hour')
  return plural(Math.floor(seconds / DAY), 'day')
}

/**
 * Seconds from an instant to now, never negative when the client clock is behind the server's.
 *
 * Takes a string as well as a `Date`: generated instants are typed `Date` but arrive as ISO strings,
 * because the client never parses a response. An instant carries its zone, so `new Date` reads it
 * exactly — unlike a calendar date, which it would shift.
 */
export const secondsSince = (instant: Date | string, now: number): number =>
  Math.max(0, Math.floor((now - new Date(instant).getTime()) / 1000))
