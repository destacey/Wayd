/**
 * A wash of the severity color for a whole row.
 *
 * The level's hex is a saturated accent. Painting it solid hides links, status tags, and
 * secondary text. A small mix leaves those in the theme colors and still marks the row.
 */
export function incidentRowTint(
  color: string | null | undefined,
): string | undefined {
  if (!color) return undefined
  return `color-mix(in srgb, ${color} 22%, transparent)`
}
