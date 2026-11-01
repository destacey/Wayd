export function formatIncidentAge(noticedAt: Date, now = Date.now()): string {
  const elapsedMs = Math.max(0, now - noticedAt.getTime())
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

export function formatIncidentProductLabel(
  products: ReadonlyArray<{ name: string }>,
): string {
  if (products.length === 0) {
    return '—'
  }

  if (products.length === 1) {
    return products[0]!.name
  }

  return `${products[0]!.name} +${products.length - 1}`
}
