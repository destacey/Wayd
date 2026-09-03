// timeline/render/svg/export-scale.ts
// Derives the time scale an export should use from the chart's live scroll
// position and viewport width.
//
// The chart's own `scale` spans the WHOLE domain (minDate→maxDate) across the
// full scrollable canvas, which for a multi-year roadmap is tens of thousands of
// px wide and mostly empty. Exporting that is correct but unrecognisable, so the
// export re-projects the currently visible slice onto a viewport-sized scale.
//
// Pure, so the behaviour can be asserted across screen sizes and zoom levels
// without a browser.

import { createTimeScale, type TimeScale } from '../../core/scale'

export interface ExportScaleInput {
  /** The chart's live scale: full domain across the full scrollable width. */
  scale: TimeScale
  /** Horizontal scroll offset within that canvas, px. */
  scrollLeft: number
  /** Visible chart width, px (excludes the group-label column). */
  viewportWidth: number
}

/**
 * The scale covering exactly what the user can currently see.
 *
 * Falls back to the full scale when the viewport has not been measured yet
 * (width 0), which happens on the very first render before the ResizeObserver
 * fires — exporting the whole domain beats exporting nothing.
 */
export function createExportScale(input: ExportScaleInput): TimeScale {
  const { scale, scrollLeft, viewportWidth } = input
  if (!(viewportWidth > 0)) return scale

  const width = Math.max(1, Math.round(viewportWidth))
  // Clamp the scroll offset into the canvas so a bounce/overscroll position
  // cannot project a window outside the domain.
  const left = Math.max(
    0,
    Math.min(scrollLeft, Math.max(0, scale.width - width)),
  )
  return createTimeScale(scale.toMs(left), scale.toMs(left + width), width)
}
