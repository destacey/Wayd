'use client'

// use-gantt-zoom.ts — the toolbar/wheel zoom state shared by every Gantt pane.
// Kept beside the engine so consumers don't each re-derive the clamp arithmetic
// (and drift on the bounds).

import { useCallback, useMemo, useState } from 'react'
import {
  DAY_MS,
  DEFAULT_PX_PER_DAY,
  MAX_PX_PER_DAY,
  MIN_PX_PER_DAY,
  ZOOM_STEP,
} from './use-gantt-pane'

export interface GanttZoomFit {
  /** Domain span the chart covers, epoch ms [start, end]. */
  domain: [number, number]
  /** Live width of the chart viewport, px. */
  viewportWidth: number
}

/**
 * Smallest px/day at which the domain still fills `viewportWidth`.
 *
 * Without this the floor is the fixed MIN_PX_PER_DAY, which knows nothing about
 * the container: a three-year domain at 1px/day is ~1,100px, so on a wide
 * monitor the user can zoom out until the chart no longer reaches the right
 * edge and the axis trails off into dead space. The roadmap timeline derives
 * the same bound as its `zoomMin`; this is the Gantt's equivalent.
 *
 * Falls back to MIN_PX_PER_DAY until the viewport has been measured.
 */
export function fitPxPerDay(fit?: GanttZoomFit): number {
  if (!fit) return MIN_PX_PER_DAY
  const [start, end] = fit.domain
  const days = (end - start) / DAY_MS
  if (!(days > 0) || !(fit.viewportWidth > 0)) return MIN_PX_PER_DAY
  // Never exceed the zoom-IN cap, or a very short domain in a wide pane would
  // produce a floor above the ceiling and strand the clamp.
  return Math.min(MAX_PX_PER_DAY, fit.viewportWidth / days)
}

export interface UseGanttZoom {
  /** Current zoom level, pixels per day. */
  pxPerDay: number
  /** Multiply the zoom by `factor`, clamped to the supported range. */
  zoomBy: (factor: number) => void
  /** Restore the default zoom. */
  resetZoom: () => void
  /** True when the zoom differs from the default (drives the reset button). */
  isZoomed: boolean
  /** True at the clamp bounds (drives the +/- button disabled states). */
  canZoomIn: boolean
  canZoomOut: boolean
  /**
   * Ctrl/Cmd+wheel handler for WaydGrid's rightPane.onWheel. Returns true when
   * it consumed the event, so the grid doesn't also scroll vertically.
   */
  onWheel: (e: React.WheelEvent | WheelEvent) => boolean
}

export function useGanttZoom(
  initial: number = DEFAULT_PX_PER_DAY,
  fit?: GanttZoomFit,
): UseGanttZoom {
  const [pxPerDay, setPxPerDay] = useState(initial)

  // Zooming out past this leaves the chart narrower than its container.
  // Destructured so the memo deps are primitives a caller cannot break by
  // passing a fresh `fit` object each render.
  const domainStart = fit?.domain[0]
  const domainEnd = fit?.domain[1]
  const viewportWidth = fit?.viewportWidth
  const minPxPerDay = useMemo(() => {
    if (domainStart === undefined || domainEnd === undefined) {
      return MIN_PX_PER_DAY
    }
    return fitPxPerDay({
      domain: [domainStart, domainEnd],
      viewportWidth: viewportWidth ?? 0,
    })
  }, [domainStart, domainEnd, viewportWidth])

  const zoomBy = useCallback(
    (factor: number) => {
      setPxPerDay((prev) =>
        Math.min(MAX_PX_PER_DAY, Math.max(minPxPerDay, prev * factor)),
      )
    },
    [minPxPerDay],
  )

  const resetZoom = useCallback(
    () => setPxPerDay(Math.max(minPxPerDay, initial)),
    [initial, minPxPerDay],
  )

  const onWheel = useCallback(
    (e: React.WheelEvent | WheelEvent) => {
      if (!e.ctrlKey && !e.metaKey) return false
      e.preventDefault()
      zoomBy(e.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP)
      return true
    },
    [zoomBy],
  )

  // The viewport can shrink (window resize, a pane opening) after the user has
  // already zoomed out, which would leave the chart short of the container.
  // Clamp on read so the rendered zoom always respects the current floor.
  const effectivePxPerDay = Math.min(
    MAX_PX_PER_DAY,
    Math.max(minPxPerDay, pxPerDay),
  )

  return {
    pxPerDay: effectivePxPerDay,
    zoomBy,
    resetZoom,
    isZoomed: effectivePxPerDay !== Math.max(minPxPerDay, initial),
    canZoomIn: effectivePxPerDay < MAX_PX_PER_DAY,
    canZoomOut: effectivePxPerDay > minPxPerDay,
    onWheel,
  }
}
