export const ONE_DAY_LABEL_MAX_CHARS = 20
const ELLIPSIS = '…'

export function truncateOneDayLabel(label: string): string {
  if (label.length <= ONE_DAY_LABEL_MAX_CHARS) {
    return label
  }

  const prefix = label.slice(0, ONE_DAY_LABEL_MAX_CHARS - ELLIPSIS.length)
  const lastSpace = prefix.lastIndexOf(' ')
  const trimmedPrefix =
    lastSpace > 0 ? prefix.slice(0, lastSpace).trimEnd() : prefix.trimEnd()

  return `${trimmedPrefix}${ELLIPSIS}`
}
