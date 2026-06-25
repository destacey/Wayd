// timeline2/core/virtualization.ts
// Visible-row windowing (NFR-3, REQUIRED). Pure function: given rows laid out
// with top/height, a scroll offset, and a viewport height, return the index
// range of rows that intersect the viewport (plus overscan). Variant-agnostic —
// runs below the layout strategy, on already-resolved rows.

export interface RowBounds {
  top: number
  height: number
}

export interface VisibleRange {
  /** First visible row index (inclusive). */
  startIndex: number
  /** Last visible row index (inclusive). -1 when nothing is visible. */
  endIndex: number
}

/**
 * Compute which rows intersect the viewport [scrollTop, scrollTop+viewportH].
 *
 * Rows must be sorted by `top` ascending and be non-overlapping (as produced by
 * a layout strategy). `overscan` adds N extra rows on each side to avoid blank
 * flashes while scrolling.
 */
export function getVisibleRange(
  rows: RowBounds[],
  scrollTop: number,
  viewportHeight: number,
  overscan = 3,
): VisibleRange {
  if (rows.length === 0 || viewportHeight <= 0) {
    return { startIndex: 0, endIndex: -1 }
  }

  const top = Math.max(0, scrollTop)
  const bottom = top + viewportHeight

  let startIndex = -1
  let endIndex = -1

  for (let i = 0; i < rows.length; i += 1) {
    const rowTop = rows[i].top
    const rowBottom = rowTop + rows[i].height
    // Intersects the viewport if it ends after the top and starts before bottom.
    if (rowBottom > top && rowTop < bottom) {
      if (startIndex === -1) startIndex = i
      endIndex = i
    } else if (startIndex !== -1 && rowTop >= bottom) {
      // Rows are sorted; once we pass the viewport bottom we can stop.
      break
    }
  }

  if (startIndex === -1) {
    // Nothing intersects (e.g. scrolled into a gap past the end).
    return { startIndex: 0, endIndex: -1 }
  }

  return {
    startIndex: Math.max(0, startIndex - overscan),
    endIndex: Math.min(rows.length - 1, endIndex + overscan),
  }
}

/** Total scrollable height = bottom of the last row (0 when empty). */
export function getTotalHeight(rows: RowBounds[]): number {
  if (rows.length === 0) return 0
  const last = rows[rows.length - 1]
  return last.top + last.height
}
