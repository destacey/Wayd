import type { Dayjs } from 'dayjs'

/**
 * A moment as the API publishes it: an ISO 8601 string in UTC (a NodaTime
 * `Instant`), such as `"2026-11-15T18:00:00.5Z"`. It stays a string in the
 * store, so RTK Query's structural sharing and CSV export see plain data.
 *
 * Branded so that it cannot be mixed up with a `CalendarDate` or an arbitrary
 * string. The generated client's `format: date-time` fields have this exact
 * type (see `orval.config.ts`).
 *
 * Order and compare instants with {@link instantTime}, never as strings: the
 * fraction of a second has as many digits as it needs, so `"…:00.5Z"` sorts
 * before `"…:00Z"`. Its date prefix is the UTC day, not the viewer's; take the
 * day with `calendarDateInZone`.
 */
export type Instant = string & { readonly __brand: 'Instant' }

/** A moment to send to the API, from a picker value or a `Date`. */
export function toInstant(value: Date | Dayjs): Instant {
  return value.toISOString() as Instant
}

/**
 * An ISO 8601 string as an instant, for one that did not come through the
 * generated client (a SignalR payload, a URL parameter, a test). Throws for a
 * string that is not a moment.
 */
export function asInstant(value: string): Instant {
  if (Number.isNaN(Date.parse(value))) {
    throw new RangeError(`"${value}" is not an ISO 8601 instant.`)
  }
  return value as Instant
}

/** The instant as epoch milliseconds, to order or compare instants. */
export function instantTime(value: Instant): number {
  return Date.parse(value)
}
