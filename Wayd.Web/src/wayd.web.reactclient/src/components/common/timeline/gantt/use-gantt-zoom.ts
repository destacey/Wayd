'use client'

// use-gantt-zoom.ts — the toolbar/wheel zoom state shared by every Gantt pane.
// Kept beside the engine so consumers don't each re-derive the clamp arithmetic
// (and drift on the bounds).

import { useCallback, useState } from 'react'
import {
  DEFAULT_PX_PER_DAY,
  MAX_PX_PER_DAY,
  MIN_PX_PER_DAY,
  ZOOM_STEP,
} from './use-gantt-pane'

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
): UseGanttZoom {
  const [pxPerDay, setPxPerDay] = useState(initial)

  const zoomBy = useCallback((factor: number) => {
    setPxPerDay((prev) =>
      Math.min(MAX_PX_PER_DAY, Math.max(MIN_PX_PER_DAY, prev * factor)),
    )
  }, [])

  const resetZoom = useCallback(() => setPxPerDay(initial), [initial])

  const onWheel = useCallback(
    (e: React.WheelEvent | WheelEvent) => {
      if (!e.ctrlKey && !e.metaKey) return false
      e.preventDefault()
      zoomBy(e.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP)
      return true
    },
    [zoomBy],
  )

  return {
    pxPerDay,
    zoomBy,
    resetZoom,
    isZoomed: pxPerDay !== initial,
    canZoomIn: pxPerDay < MAX_PX_PER_DAY,
    canZoomOut: pxPerDay > MIN_PX_PER_DAY,
    onWheel,
  }
}
