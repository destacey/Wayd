// timeline/core/geometry.ts
// Pure pixel-box computation: given a TimeScale, a resolved row, and lane sizing,
// produce absolute {left, top, width, height} boxes for items and backgrounds.
// No React, no DOM. Consumed by the render layer.

import type { ResolvedRow, TimelineItem } from './types'
import type { TimeScale } from './scale'

export interface Box {
  left: number
  top: number
  width: number
  height: number
}

export interface ItemBox extends Box {
  item: TimelineItem
}

/** Minimum rendered width so zero/short-duration ranges stay clickable. */
const MIN_RANGE_WIDTH = 4
/** Milliseconds in a day — a range shorter than this is a single-day item. */
const ONE_DAY_MS = 24 * 60 * 60 * 1000
/** Milestone diamonds are square; sized to the lane height at render time. */

export interface GeometryConfig {
  laneHeight: number
  /** Inset within a lane so bars don't touch lane edges, px. */
  lanePadding: number
  /** Padding added below the bar area in each row (from the layout). Half of
   *  this is also added above so bars are vertically centred in the row. */
  rowPadding?: number
}

/**
 * Box for a single item placed at `lane` within `row`.
 * Range: spans [start,end) in x. Milestone: zero-duration, centered on start
 * (the render layer draws a diamond of `height` width around left).
 */
export function itemBox(
  item: TimelineItem,
  lane: number,
  row: ResolvedRow,
  scale: TimeScale,
  config: GeometryConfig,
): ItemBox {
  // Offset bars below any reserved headroom (e.g. a timebox label band), plus
  // half the row padding so bars are vertically centred (the other half sits
  // below the bar area as breathing room between rows).
  const halfRowPad = (config.rowPadding ?? 0) / 2
  const top =
    row.top + (row.topInset ?? 0) + halfRowPad + lane * config.laneHeight + config.lanePadding
  const height = config.laneHeight - config.lanePadding * 2

  if (item.kind === 'milestone') {
    const center = Math.max(0, Math.min(scale.width, scale.toX(item.start)))
    return { item, left: center, top, width: 0, height }
  }

  // Clamp the bar to the rendered domain [0, scale.width]. Items can extend past
  // the hard bounds (min/max); we cut them at the edge rather than let them
  // overflow the canvas into a gridline-less, scrollable void (legacy behavior).
  const rawX1 = scale.toX(item.start)
  const rawX2 = scale.toX(item.end)
  const x1 = Math.max(0, Math.min(scale.width, rawX1))
  const x2 = Math.max(0, Math.min(scale.width, rawX2))
  // Does the (unclamped) item overlap the visible domain at all? A point item
  // (start === end) counts as intersecting when it sits within [0, width].
  const intersects = rawX2 >= 0 && rawX1 <= scale.width
  // A one-day (or shorter) range is too narrow to be a useful bar, so render it
  // as a small square (its height) — an easy click target, with the label drawn
  // beside it. Longer ranges keep the slim min-width floor.
  const isOneDay = item.end - item.start <= ONE_DAY_MS
  const minWidth = isOneDay ? height : MIN_RANGE_WIDTH
  // Outside the domain → zero width (nothing to show). Inside → keep the
  // min-width floor so short/zero-duration ranges remain visible/clickable.
  const width = intersects ? Math.max(minWidth, x2 - x1) : 0
  return { item, left: x1, top, width, height }
}

/**
 * Box for a background region spanning a row (or the whole chart body when
 * `fullHeight` is given). Backgrounds ignore lanes — they sit behind items.
 */
export function backgroundBox(
  item: TimelineItem,
  row: ResolvedRow | null,
  scale: TimeScale,
  fullHeight?: number,
): Box {
  // Clamp to the rendered domain so a timebox extending past the bounds is cut
  // at the edge (same as item bars), never overflowing the canvas.
  const x1 = Math.max(0, Math.min(scale.width, scale.toX(item.start)))
  const x2 = Math.max(0, Math.min(scale.width, scale.toX(item.end)))
  const width = Math.max(0, x2 - x1)
  if (row) {
    return { left: x1, top: row.top, width, height: row.height }
  }
  return { left: x1, top: 0, width, height: fullHeight ?? 0 }
}

/** Clamp a box's horizontal span to the visible [0,width] viewport (for culling). */
export function isHorizontallyVisible(
  box: Box,
  viewportWidth: number,
): boolean {
  return box.left + box.width >= 0 && box.left <= viewportWidth
}

/**
 * Grow rows whose group label (measured in the rendered column) is taller than
 * the lane-based height, then re-stack so tops stay contiguous. Pure: takes
 * measured label heights (keyed by row identity — rowKey, falling back to
 * groupId) and returns adjusted rows + new total height. When no row needs
 * growing, returns the input rows unchanged (same reference) so callers can skip
 * a re-render.
 */
export function growRowsForLabels(
  rows: ResolvedRow[],
  labelHeights: Map<string, number>,
): { rows: ResolvedRow[]; totalHeight: number } {
  let changed = false
  let top = 0
  const adjusted = rows.map((row) => {
    const measureKey = row.rowKey ?? row.groupId
    const labelH = measureKey ? labelHeights.get(measureKey) ?? 0 : 0
    const height = Math.max(row.height, labelH)
    const next =
      height !== row.height || top !== row.top ? { ...row, top, height } : row
    if (next !== row) changed = true
    top += height
    return next
  })
  return changed ? { rows: adjusted, totalHeight: top } : { rows, totalHeight: top }
}
