'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * A width change smaller than this is noise — a scrollbar appearing, a
 * sub-pixel layout settle — and remounting the chart for it would flicker.
 */
const WIDTH_CHANGE_THRESHOLD_PX = 8

/**
 * How long the width must hold still before the chart remounts.
 *
 * Dragging the facts rail emits a resize per frame; without this the chart is
 * destroyed and rebuilt on every one of them. Remounting once the drag settles
 * costs a redraw the user has already stopped waiting on.
 */
const SETTLE_MS = 150

export interface ChartRemountOnResize {
  /** Attach to the element wrapping the chart. */
  ref: (node: HTMLDivElement | null) => void
  /** Pass as the chart's `key` so a settled width change remounts it. */
  renderKey: number
}

/**
 * Makes an `@ant-design/charts` (G2 v5) chart follow its container's width.
 *
 * `autoFit: true` sizes a chart correctly when it mounts, but afterwards only
 * re-fits on the **window**'s `resize` event. Anything that changes the chart's
 * width without resizing the window — opening the record facts rail, dragging
 * it wider, collapsing the app sider — never reaches it, so the canvas keeps
 * its last size and overflows or under-fills its container.
 *
 * `autoFit: true` is still required on the chart — this hook only tells it
 * when to reconsider.
 */
export const useChartRemountOnResize = (): ChartRemountOnResize => {
  const [renderKey, setRenderKey] = useState(0)
  const lastWidthRef = useRef(0)
  const observerRef = useRef<ResizeObserver | null>(null)
  const timerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  // A callback ref, not `useRef` + `useEffect([])`: the wrapper does not always
  // exist when an effect would first run. `ChartCard` renders an antd `Card`
  // with `loading`, which swaps the body for a skeleton and mounts the real
  // content only once loading clears — so an effect keyed on `[]` captures a
  // null ref and silently observes nothing. This re-attaches whenever the node
  // actually changes, including across that swap.
  const ref = useCallback((node: HTMLDivElement | null) => {
    observerRef.current?.disconnect()
    observerRef.current = null
    clearTimeout(timerRef.current)

    // Observe the PARENT, not the wrapper the chart renders into. Remounting
    // the chart reflows its own wrapper — a legend re-wrapping, a canvas
    // rounding a fraction of a pixel — which the observer would read as another
    // resize and remount again, a loop that took seconds to damp out. The
    // parent is sized by the layout above it, so nothing the chart does can
    // feed back into it.
    const target = node?.parentElement
    if (!target) return

    lastWidthRef.current = target.getBoundingClientRect().width

    const observer = new ResizeObserver((entries) => {
      const width = entries[0]?.contentRect.width ?? 0
      if (Math.abs(width - lastWidthRef.current) < WIDTH_CHANGE_THRESHOLD_PX) {
        return
      }

      lastWidthRef.current = width
      // Restart the clock on every qualifying change, so a drag remounts once
      // when it stops rather than once per frame.
      clearTimeout(timerRef.current)
      timerRef.current = setTimeout(() => setRenderKey((k) => k + 1), SETTLE_MS)
    })

    observer.observe(target)
    observerRef.current = observer
  }, [])

  useEffect(
    () => () => {
      observerRef.current?.disconnect()
      clearTimeout(timerRef.current)
    },
    [],
  )

  return { ref, renderKey }
}

export default useChartRemountOnResize
