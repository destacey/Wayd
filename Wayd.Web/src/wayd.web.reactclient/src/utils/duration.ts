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
): string => {
  const totalSeconds = Math.max(
    0,
    Math.floor((new Date(end).getTime() - new Date(start).getTime()) / 1000),
  )
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  if (hours > 0) return `${hours}h ${String(minutes).padStart(2, '0')}m`
  if (minutes > 0) return `${minutes}m ${String(seconds).padStart(2, '0')}s`
  return `${seconds}s`
}
