// timeline/render/capture-limits.ts
// Canvas budgeting for the PNG export. Browsers cap both a canvas's individual
// side length and its total area; exceeding either yields a BLANK canvas rather
// than an exception, so an unbudgeted rasterise fails silently. Pure, so these
// are unit-testable without a DOM.
//
// The SVG export has no such limits — these apply only when rasterising it.

/**
 * Largest side, px, we allow a canvas to reach. Chrome/Edge allow 65,535 and
 * Firefox 32,767, but Safari caps at 16,384 — take the floor so the export
 * behaves identically everywhere.
 */
export const MAX_CANVAS_SIDE = 16_384

/**
 * Largest total area, device px, we allow a canvas to reach. Chrome's practical
 * ceiling is ~268M (16,384²); back off ~25% for headroom.
 */
export const MAX_CANVAS_AREA = 200_000_000

/**
 * Highest device-pixel scale at which a `width` x `height` (CSS px) image still
 * fits the canvas budget. Returns at least 1 — below that the output stops
 * being usable, so a capture that cannot fit even at 1x is refused instead (see
 * `exceedsBudgetAtAnyScale`).
 */
export function fitScaleToBudget(
  width: number,
  height: number,
  requestedScale: number,
): number {
  if (width <= 0 || height <= 0) return requestedScale
  const bySide = Math.min(MAX_CANVAS_SIDE / width, MAX_CANVAS_SIDE / height)
  const byArea = Math.sqrt(MAX_CANVAS_AREA / (width * height))
  return Math.max(1, Math.min(requestedScale, bySide, byArea))
}

/**
 * True when `width` x `height` (CSS px) exceeds a single canvas even at 1x, so
 * no scale can rasterise it. `fitScaleToBudget` floors at 1 to keep output
 * legible, which means it CANNOT express this case — callers must check here
 * first and fail with a useful message rather than handing the browser an
 * over-cap canvas that comes back blank.
 */
export function exceedsBudgetAtAnyScale(
  width: number,
  height: number,
): boolean {
  if (width <= 0 || height <= 0) return false
  return (
    width > MAX_CANVAS_SIDE ||
    height > MAX_CANVAS_SIDE ||
    width * height > MAX_CANVAS_AREA
  )
}
