export function instantToEpochMs(instant: Date | string): number {
  return instant instanceof Date ? instant.getTime() : new Date(instant).getTime()
}

/** RTK Query and NSwag often deliver instants as ISO strings after cache round-trips. */
export function coerceIncidentInstant(instant: Date | string): Date {
  return instant instanceof Date ? instant : new Date(instant)
}

export function formatIncidentAge(noticedAt: Date | string, now = Date.now()): string {
  const noticedMs = instantToEpochMs(noticedAt)
  if (Number.isNaN(noticedMs)) {
    return '—'
  }

  const elapsedMs = Math.max(0, now - noticedMs)
  const minutes = Math.floor(elapsedMs / 60_000)
  if (minutes < 60) {
    return `${minutes}m`
  }

  const hours = Math.floor(minutes / 60)
  if (hours < 24) {
    return `${hours}h`
  }

  const days = Math.floor(hours / 24)
  return `${days}d`
}

/**
 * A stretch already measured in milliseconds. A real zero stays visible as
 * seconds, so it is not read as "not started".
 */
export function formatIncidentDuration(milliseconds: number): string {
  const totalSeconds = Math.max(0, Math.round(milliseconds / 1000))
  if (totalSeconds < 60) {
    return `${totalSeconds}s`
  }

  const minutes = Math.floor(totalSeconds / 60)
  if (minutes < 60) {
    return `${minutes}m`
  }

  const hours = Math.floor(minutes / 60)
  const remainingMinutes = minutes % 60
  if (hours < 48) {
    return remainingMinutes === 0 ? `${hours}h` : `${hours}h ${remainingMinutes}m`
  }

  const days = Math.floor(hours / 24)
  const remainingHours = hours % 24
  return remainingHours === 0 ? `${days}d` : `${days}d ${remainingHours}h`
}
