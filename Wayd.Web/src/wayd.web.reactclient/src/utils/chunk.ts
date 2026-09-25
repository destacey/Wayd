/**
 * Splits a list into consecutive slices of at most `size` items, in order.
 * An empty list gives no slices; a non-positive size gives one slice of all.
 */
export const chunk = <T>(items: readonly T[], size: number): T[][] => {
  if (items.length === 0) return []
  if (size <= 0) return [[...items]]
  const slices: T[][] = []
  for (let i = 0; i < items.length; i += size) {
    slices.push(items.slice(i, i + size))
  }
  return slices
}
