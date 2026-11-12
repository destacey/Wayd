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
