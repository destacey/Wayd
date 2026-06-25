// timeline2/core/zoom.ts
// Hand-rolled pan/zoom transform (decision D-2). Pure math, no DOM/React.
//
// The chart's horizontal extent is `baseWidth * zoom` pixels mapping the full
// time domain. Panning is native horizontal scroll (scrollLeft); this module
// owns ZOOM: clamping the factor and recomputing scrollLeft so a chosen anchor
// point (cursor or viewport centre) stays fixed under the time axis as we zoom.

/** Bounds for the zoom factor (multiplier over the base, fit-to-window width). */
export interface ZoomBounds {
  /** Smallest factor — fully zoomed out. Usually 1 (base = fit the window). */
  min: number
  /** Largest factor — fully zoomed in (derived from zoomMin; see maxZoom). */
  max: number
}

/** Clamp a zoom factor into [min, max]. */
export function clampZoom(zoom: number, bounds: ZoomBounds): number {
  if (!Number.isFinite(zoom)) return bounds.min
  return Math.min(bounds.max, Math.max(bounds.min, zoom))
}

/**
 * Largest allowed zoom factor so the viewport still spans at least `zoomMinMs`
 * of time (vis-timeline's `zoomMin`, e.g. 1 day). Below the base factor (zoomed
 * further out than the window) is never allowed, so the result is >= 1.
 *
 *   At factor f, chartWidth = baseWidth * f, pxPerMs = chartWidth / domainMs,
 *   viewportMs = viewportWidth / pxPerMs = viewportWidth * domainMs / chartWidth.
 *   Require viewportMs >= zoomMinMs  =>  f <= viewportWidth * domainMs /
 *                                              (zoomMinMs * baseWidth).
 */
export function maxZoom(
  baseWidth: number,
  viewportWidth: number,
  domainMs: number,
  zoomMinMs: number,
): number {
  if (baseWidth <= 0 || zoomMinMs <= 0 || viewportWidth <= 0) return 1
  const cap = (viewportWidth * domainMs) / (zoomMinMs * baseWidth)
  return Math.max(1, cap)
}

/**
 * New scrollLeft that keeps the time currently under `anchorX` (viewport-local
 * px, i.e. distance from the viewport's left edge) fixed after zooming.
 *
 * The content offset under the anchor is `scrollLeft + anchorX`. As widths scale
 * from `oldWidth` to `newWidth`, that offset scales by `newWidth / oldWidth`;
 * subtract the anchor to get the new scrollLeft. Clamped to a valid range.
 */
export function anchoredScrollLeft(params: {
  oldWidth: number
  newWidth: number
  oldScrollLeft: number
  anchorX: number
  viewportWidth: number
}): number {
  const { oldWidth, newWidth, oldScrollLeft, anchorX, viewportWidth } = params
  if (oldWidth <= 0) return 0
  const contentX = oldScrollLeft + anchorX
  const scaled = (contentX * newWidth) / oldWidth
  const next = scaled - anchorX
  const maxScroll = Math.max(0, newWidth - viewportWidth)
  return Math.min(maxScroll, Math.max(0, next))
}
