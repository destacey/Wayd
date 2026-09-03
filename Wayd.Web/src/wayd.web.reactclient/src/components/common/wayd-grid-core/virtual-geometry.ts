// virtual-geometry.ts — mapping the row virtualizer's estimated geometry onto
// the heights the grid's rows actually render at.
//
// The virtualizer sizes every row with a fixed estimate, but real rows flow at
// a content-driven height (29px against a 28px estimate is typical). Anything
// positioned from the estimate — the offset spacers, and a chart pane's bars —
// has to be scaled by the same factor, or the two disagree and the error grows
// with scroll depth.

/** Scale from the virtualizer's estimated row height to the measured one. */
export function rowScaleFor(
  measuredRowHeight: number | null,
  estimate: number,
): number {
  if (!measuredRowHeight || estimate <= 0) return 1
  return measuredRowHeight / estimate
}

export interface VirtualSpacerInput {
  /** Offset of the first rendered row, in ESTIMATED px. */
  firstRowStart: number
  /** End of the last rendered row, in ESTIMATED px. */
  lastRowEnd: number
  /** Total content size the virtualizer reports, in ESTIMATED px. */
  totalSize: number
  /** Estimate-to-measured scale (see `rowScaleFor`). */
  rowScale: number
  /** False when no rows are rendered (empty grid). */
  hasRows: boolean
}

/**
 * Heights for the spacer rows that stand in for unrendered rows above and
 * below the window, in REAL px.
 *
 * Scaling matters: the spacers sit in the same table as rows that render at
 * their natural height, so an unscaled spacer makes the grid's content height a
 * mix of two coordinate spaces. A chart pane positions its bars wholly in
 * scaled space, so the mismatch shows up as bars drifting further from their
 * rows the further down the user scrolls.
 */
export function virtualSpacers(input: VirtualSpacerInput): {
  top: number
  bottom: number
} {
  if (!input.hasRows) return { top: 0, bottom: 0 }
  return {
    top: input.firstRowStart * input.rowScale,
    bottom: (input.totalSize - input.lastRowEnd) * input.rowScale,
  }
}
