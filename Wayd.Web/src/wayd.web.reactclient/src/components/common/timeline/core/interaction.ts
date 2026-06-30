// timeline/core/interaction.ts
// Pure drag math: translate a horizontal pixel delta into a date change, with
// day-snapping and domain clamping. No DOM, no React — the testable core of
// move / resize-start / resize-end (FR-5). The hook (useBarDrag) handles pointer
// plumbing and calls these.

import dayjs from 'dayjs'
import type { TimelineItem } from './types'

export type DragMode = 'move' | 'resize-start' | 'resize-end'

const DAY = 86_400_000

/** Snap an epoch-ms value to the start of its UTC-agnostic local day. */
export function snapToDay(ms: number): number {
  return dayjs(ms).startOf('day').valueOf()
}

export interface DragContext {
  mode: DragMode
  /** The item being dragged (original, unmodified bounds). */
  item: Pick<TimelineItem, 'start' | 'end' | 'kind'>
  /** Pixels per millisecond, from the active scale. */
  pxPerMs: number
  /** Horizontal pixel delta since drag start. */
  deltaPx: number
  /** Optional hard bounds (epoch ms) the result is clamped to. */
  min?: number
  max?: number
  /** Snap result to day boundaries (default true). */
  snap?: boolean
}

export interface DragResult {
  start: number
  end: number
}

/**
 * Compute the new [start,end] for a drag. Pure.
 *  - move:         shift both ends by the delta.
 *  - resize-start: move start only; never past end (min 1 day span).
 *  - resize-end:   move end only; never before start (min 1 day span).
 * Day-snaps (unless disabled) and clamps to [min,max] if given.
 */
export function applyDrag(ctx: DragContext): DragResult {
  const { mode, item, pxPerMs, deltaPx, min, max, snap = true } = ctx
  const deltaMs = pxPerMs > 0 ? deltaPx / pxPerMs : 0
  const maybeSnap = (ms: number) => (snap ? snapToDay(ms) : ms)
  const clamp = (ms: number) => {
    let v = ms
    if (min != null && v < min) v = min
    if (max != null && v > max) v = max
    return v
  }

  let start = item.start
  let end = item.end

  if (mode === 'move') {
    start = clamp(maybeSnap(item.start + deltaMs))
    // Preserve calendar-day duration on move. The backend works with LocalDate
    // deltas, so preserving raw milliseconds can turn a move into a resize when
    // the range crosses a daylight-saving boundary.
    const durationDays = dayjs(item.end)
      .startOf('day')
      .diff(dayjs(item.start).startOf('day'), 'day')
    end = dayjs(start).add(durationDays, 'day').valueOf()
    if (max != null && end > max) {
      end = maybeSnap(max)
      start = dayjs(end).subtract(durationDays, 'day').valueOf()
    }
  } else if (mode === 'resize-start') {
    start = clamp(maybeSnap(item.start + deltaMs))
    // Keep at least a one-day span.
    if (start > end - DAY) start = end - DAY
  } else {
    // resize-end
    end = clamp(maybeSnap(item.end + deltaMs))
    if (end < start + DAY) end = start + DAY
  }

  return { start, end }
}

/**
 * Convert a horizontal pixel position within a bar into a progress percent
 * (0..100), given the bar's left edge and pixel width. Pure.
 */
export function progressFromX(
  pointerX: number,
  barLeft: number,
  barWidth: number,
): number {
  if (barWidth <= 0) return 0
  const ratio = (pointerX - barLeft) / barWidth
  return Math.round(Math.max(0, Math.min(1, ratio)) * 100)
}
