import type { CSSProperties } from 'react'
import type { Column, Header } from '@tanstack/react-table'

/**
 * Sticky-rendering facts for a pinned column's cells, derived from TanStack's
 * columnPinning state. TanStack owns the state model (and reorders pinned
 * columns to the table edges in getVisibleLeafColumns / getVisibleCells /
 * getHeaderGroups); the grid renders the stick itself: `position: sticky`
 * with these offsets on the pinned `th`s and `td`s in BOTH tables.
 */
export interface PinnedColumnOffsets {
  side: 'left' | 'right'
  /** Sticky inset in px — `left` for left-pinned, `right` for right-pinned:
   *  the summed widths of the other pinned columns between this one and its
   *  table edge. */
  offset: number
  /** Last left-pinned / first right-pinned column — where the grid draws the
   *  divider edge between the pinned and scrolling sections. */
  isEdge: boolean
}

/** Pinned offsets for a leaf column's cells, or undefined when unpinned. */
export function getPinnedOffsets<T>(
  column: Column<T, unknown>,
): PinnedColumnOffsets | undefined {
  const side = column.getIsPinned()
  if (!side) return undefined
  return side === 'left'
    ? {
        side,
        offset: column.getStart('left'),
        isEdge: column.getIsLastColumn('left'),
      }
    : {
        side,
        offset: column.getAfter('right'),
        isEdge: column.getIsFirstColumn('right'),
      }
}

/**
 * Pinned offsets for a grouped-header band cell. TanStack splits a group
 * spanning pinned + unpinned leaves into one band cell per pin section, so a
 * rendered band cell's leaves are single-section by construction — but guard
 * anyway: a mixed or unpinned band renders unpinned.
 *
 * The band sticks at its leftmost (left-pinned) / rightmost (right-pinned)
 * leaf's offset and draws the divider edge when it contains the edge leaf.
 */
export function getPinnedBandOffsets<T>(
  header: Header<T, unknown>,
): PinnedColumnOffsets | undefined {
  const leaves = header.getLeafHeaders()
  const columns =
    leaves.length > 0 ? leaves.map((leaf) => leaf.column) : [header.column]
  const side = columns[0].getIsPinned()
  if (!side || columns.some((col) => col.getIsPinned() !== side)) {
    return undefined
  }
  return side === 'left'
    ? {
        side,
        offset: Math.min(...columns.map((col) => col.getStart('left'))),
        isEdge: columns.some((col) => col.getIsLastColumn('left')),
      }
    : {
        side,
        offset: Math.min(...columns.map((col) => col.getAfter('right'))),
        isEdge: columns.some((col) => col.getIsFirstColumn('right')),
      }
}

/** The inline sticky inset for a pinned cell (`left` or `right`), or
 *  undefined when unpinned. Merged into the cell's `style`. */
export function pinnedCellStyle(
  offsets: PinnedColumnOffsets | undefined,
): CSSProperties | undefined {
  if (!offsets) return undefined
  return offsets.side === 'left'
    ? { left: offsets.offset }
    : { right: offsets.offset }
}

/**
 * CSS-module class names the owning grid supplies for pinned cells: the base
 * sticky/background class plus the divider-edge variants.
 */
export interface PinnedCellClasses {
  pinned: string
  pinnedLeftEdge: string
  pinnedRightEdge: string
}

/** The pinned classes for a cell as one string ('' when unpinned). */
export function pinnedCellClassNames(
  offsets: PinnedColumnOffsets | undefined,
  classes: PinnedCellClasses,
): string {
  if (!offsets) return ''
  if (!offsets.isEdge) return classes.pinned
  return `${classes.pinned} ${
    offsets.side === 'left' ? classes.pinnedLeftEdge : classes.pinnedRightEdge
  }`
}
