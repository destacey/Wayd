// virtual-geometry.ts — the spacer rows that stand in for the rows the
// virtualizer has not rendered.

export interface VirtualSpacerInput {
  /** Offset of the first rendered row, px. */
  firstRowStart: number
  /** End of the last rendered row, px. */
  lastRowEnd: number
  /** Total content size the virtualizer reports, px. */
  totalSize: number
  /** False when no rows are rendered (empty grid). */
  hasRows: boolean
}

/**
 * Heights for the spacer rows above and below the rendered window, so the
 * body's content height is the virtualizer's total size.
 */
export function virtualSpacers(input: VirtualSpacerInput): {
  top: number
  bottom: number
} {
  if (!input.hasRows) return { top: 0, bottom: 0 }
  return {
    top: input.firstRowStart,
    bottom: input.totalSize - input.lastRowEnd,
  }
}
