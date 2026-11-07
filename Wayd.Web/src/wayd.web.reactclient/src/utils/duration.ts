/**
 * A length of time as a short duration: "45s", "2m 14s", "1h 05m". A negative
 * length reads as "0s".
 */
const formatSeconds = (totalSeconds: number): string => {
  const seconds = Math.max(0, Math.floor(totalSeconds))
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  const rest = seconds % 60

  if (hours > 0) return `${hours}h ${String(minutes).padStart(2, '0')}m`
  if (minutes > 0) return `${minutes}m ${String(rest).padStart(2, '0')}s`
  return `${rest}s`
}

/**
 * The time between two instants as a short duration: "45s", "2m 14s",
 * "1h 05m". `end` defaults to now, for something still running. A negative
 * span (a server clock slightly ahead of the browser's) reads as "0s".
 *
 * Takes strings too: the generated client types an Instant as `Date`, but the
 * response is parsed as plain JSON, so at runtime it is the ISO string.
 */
export const formatDuration = (
  start: Date | string,
  end: Date | string = new Date(),
): string =>
  formatSeconds((new Date(end).getTime() - new Date(start).getTime()) / 1000)

/**
 * A measured run time in milliseconds as a short duration. Under a second it
 * keeps the milliseconds ("350ms"), since many jobs finish that fast and "0s"
 * would hide the difference; from a second up it reads like
 * {@link formatDuration}.
 */
export const formatMilliseconds = (milliseconds: number): string =>
  milliseconds < 1000
    ? `${Math.max(0, Math.round(milliseconds))}ms`
    : formatSeconds(milliseconds / 1000)
