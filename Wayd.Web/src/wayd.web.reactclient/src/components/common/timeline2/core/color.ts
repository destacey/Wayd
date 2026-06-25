// timeline2/core/color.ts
// Tiny contrast helper: choose readable text color for a given bar background.
// Pure, no deps. Matches the luminance approach the legacy timeline used.

/** Relative luminance (0..1) of a #rrggbb / #rgb hex color; 1 for unknown. */
export function luminance(hex: string): number {
  let h = hex.trim().replace('#', '')
  if (h.length === 3) {
    h = h
      .split('')
      .map((c) => c + c)
      .join('')
  }
  if (h.length !== 6) return 1
  const r = parseInt(h.slice(0, 2), 16) / 255
  const g = parseInt(h.slice(2, 4), 16) / 255
  const b = parseInt(h.slice(4, 6), 16) / 255
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

/** Black for light backgrounds, white for dark — readable bar text. */
export function contrastText(backgroundHex: string | undefined): string {
  if (!backgroundHex) return '#ffffff'
  return luminance(backgroundHex) > 0.6 ? '#1f1f1f' : '#ffffff'
}
