const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 60 * 60],
  ['month', 30 * 24 * 60 * 60],
  ['week', 7 * 24 * 60 * 60],
  ['day', 24 * 60 * 60],
  ['hour', 60 * 60],
  ['minute', 60],
]

const formatter = new Intl.RelativeTimeFormat('en', { numeric: 'auto' })

/**
 * How long ago an instant was, in the largest whole unit: "yesterday",
 * "2 hours ago", "3 weeks ago". Anything under a minute is "just now".
 *
 * Takes a string too: the generated client types an Instant as `Date`, but the
 * response is parsed as plain JSON, so at runtime it is the ISO string.
 */
export const formatTimeAgo = (
  instant: Date | string,
  now: Date = new Date(),
): string => {
  const seconds = Math.round(
    (now.getTime() - new Date(instant).getTime()) / 1000,
  )

  for (const [unit, size] of units) {
    if (Math.abs(seconds) >= size) {
      return formatter.format(-Math.floor(seconds / size), unit)
    }
  }

  return 'just now'
}
