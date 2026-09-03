'use client'

// use-chart-pane-width.ts — live width of the WaydGrid chart pane.
//
// The zoom floor has to know how wide the chart viewport actually is, and that
// is not derivable from state: WaydGrid seeds the pane from `defaultWidth` and
// then mutates the DOM directly while the divider is dragged, committing to
// state only on release. Observing the element is the only way to track it
// through a drag, a window resize, or a sidebar toggle.

import { useEffect, useState } from 'react'

/** Marks the pane in WaydGrid; kept in sync with wayd-grid.tsx. */
const CHART_PANE_SELECTOR = '[data-wayd-grid-chart-pane]'

/**
 * Width of the chart pane in px, or 0 until it is measured (or when the grid
 * is showing no chart). `enabled` lets a caller skip the observer entirely
 * while the chart is toggled off.
 */
export function useChartPaneWidth(enabled = true): number {
  const [width, setWidth] = useState(0)

  useEffect(() => {
    if (!enabled) return

    const el = document.querySelector<HTMLElement>(CHART_PANE_SELECTOR)
    if (!el) return

    // Without ResizeObserver (jsdom, older browsers), take a single
    // measurement rather than throwing — the zoom floor is then fixed at the
    // pane's initial width instead of being lost entirely. Matches the guard
    // WaydGrid uses for its own observers.
    //
    // Deferred to a microtask so the write lands outside the effect body, the
    // same reason the observed path reports through its callback.
    if (typeof ResizeObserver === 'undefined') {
      let cancelled = false
      queueMicrotask(() => {
        if (!cancelled) setWidth(el.getBoundingClientRect().width)
      })
      return () => {
        cancelled = true
      }
    }

    // ResizeObserver fires once on observe, so the initial measurement arrives
    // through the callback rather than a synchronous setState in the effect
    // body (which would cascade a render).
    const observer = new ResizeObserver(() =>
      setWidth(el.getBoundingClientRect().width),
    )
    observer.observe(el)
    return () => observer.disconnect()
  }, [enabled])

  // Report 0 while the chart is hidden without storing it: the pane is
  // unmounted then, and clearing state in the effect would cascade a render.
  return enabled ? width : 0
}
